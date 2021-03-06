﻿#region Copyright
// // -----------------------------------------------------------------------
// // <copyright company="cdmdotnet Limited">
// // 	Copyright cdmdotnet Limited. All rights reserved.
// // </copyright>
// // -----------------------------------------------------------------------
#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using cdmdotnet.Logging;
using Cqrs.Events;
using Microsoft.AspNet.SignalR;

namespace Cqrs.WebApi.SignalR.Hubs
{
	public class NotificationHub
		: Hub
		, INotificationHub
	{
		public NotificationHub(ILogger logger, ICorrelationIdHelper correlationIdHelper)
		{
			Logger = logger;
			CorrelationIdHelper = correlationIdHelper;
		}

		public NotificationHub()
		{
		}

		public ILogger Logger { get; set; }

		public ICorrelationIdHelper CorrelationIdHelper { get; set; }

		public Func<string, Guid> ConvertUserTokenToUserRsn { get; set; }

		#region Overrides of HubBase

		/// <summary>
		/// When the connection connects to this hub instance we register the connection so we can respond back to it.
		/// </summary>
		public override Task OnConnected()
		{
			return Join();
		}

		/// <summary>
		/// When the connection reconnects to this hub instance we register the connection so we can respond back to it.
		/// </summary>
		public override Task OnReconnected()
		{
			return Join();
		}

		#endregion

		protected virtual string UserToken()
		{
			string userRsn = Context.RequestCookies["X-Token"].Value;

			return userRsn.Replace(".", string.Empty);
		}

		protected virtual Task Join()
		{
			string userToken = UserToken();
			string connectionId = Context.ConnectionId;
			return Task.Factory.StartNew(() =>
			{
				Groups.Add(connectionId, string.Format("User-{0}", userToken)).Wait();

				CurrentHub
					.Clients
					.Group(string.Format("User-{0}", userToken))
					.registered("User: " + userToken);

				if (ConvertUserTokenToUserRsn != null)
				{
					try
					{
						Guid userRsn = ConvertUserTokenToUserRsn(userToken);
						Groups.Add(connectionId, string.Format("UserRsn-{0}", userRsn)).Wait();

						CurrentHub
							.Clients
							.Group(string.Format("UserRsn-{0}", userRsn))
							.registered("UserRsn: " + userRsn);

					}
					catch (Exception exception)
					{
						Logger.LogWarning(string.Format("Registering user token '{0}' to a user RSN and into the SignalR group failed.", userToken), exception: exception, metaData: GetAdditionalDataForLogging(userToken));
					}
				}
			});
		}

		protected virtual IHubContext CurrentHub
		{
			get
			{
				return GlobalHost.ConnectionManager.GetHubContext<NotificationHub>();
			}
		}

		/// <summary>
		/// Send out an event to specific user RSNs
		/// </summary>
		void INotificationHub.SendUsersEvent<TSingleSignOnToken>(IEvent<TSingleSignOnToken> eventData, params Guid[] userRsnCollection)
		{
			IList<Guid> optimisedUserRsnCollection = (userRsnCollection ?? Enumerable.Empty<Guid>()).ToList();

			Logger.LogDebug(string.Format("Sending a message on the hub for user RSNs [{0}].", string.Join(", ", optimisedUserRsnCollection)));

			try
			{
				var tokenSource = new CancellationTokenSource();
				Guid correlationId = CorrelationIdHelper.GetCorrelationId();
				Task.Run
				(
					() =>
					{
						// Since we're crossing threads, we need to pass the correlationId
						try
						{
							CorrelationIdHelper.SetCorrelationId(correlationId);
						}
						catch (Exception exception)
						{
							foreach (Guid userRsn in optimisedUserRsnCollection)
								Logger.LogWarning("Transferring CorrelationId across a thread failed.", exception: exception, metaData: GetAdditionalDataForLogging(userRsn));
						}

						foreach (Guid userRsn in optimisedUserRsnCollection)
						{
							var metaData = GetAdditionalDataForLogging(userRsn);
							try
							{
								Clients
									.Group(string.Format("UserRsn-{0}", userRsn))
									.notifyEvent(eventData);
							}
							catch (TimeoutException exception)
							{
								Logger.LogWarning("Sending a message on the hub timed-out.", exception: exception, metaData: metaData);
							}
							catch (Exception exception)
							{
								Logger.LogError("Sending a message on the hub resulted in an error.", exception: exception, metaData: metaData);
							}
						}
					}, tokenSource.Token
				);

				tokenSource.CancelAfter(15 * 1000);
			}
			catch (Exception exception)
			{
				foreach (Guid userRsn in optimisedUserRsnCollection)
					Logger.LogError("Queueing a message on the hub resulted in an error.", exception: exception, metaData: GetAdditionalDataForLogging(userRsn));
			}
		}

		/// <summary>
		/// Send out an event to specific user token
		/// </summary>
		void INotificationHub.SendUserEvent<TSingleSignOnToken>(IEvent<TSingleSignOnToken> eventData, string userToken)
		{
			Logger.LogDebug(string.Format("Sending a message on the hub for user [{0}].", userToken));

			try
			{
				var tokenSource = new CancellationTokenSource();
				Guid correlationId = CorrelationIdHelper.GetCorrelationId();
				Task.Run
				(
					() =>
					{
						var metaData = GetAdditionalDataForLogging(userToken);

						// Since we're crossing threads, we need to pass the correlationId
						try
						{
							CorrelationIdHelper.SetCorrelationId(correlationId);
						}
						catch (Exception exception)
						{
							Logger.LogWarning("Transferring CorrelationId across a thread failed.", exception: exception, metaData: metaData);
						}

						try
						{
							CurrentHub
								.Clients
								.Group(string.Format("User-{0}", userToken))
								.notifyEvent(new { Type = eventData.GetType().FullName, Data = eventData });
						}
						catch (TimeoutException exception)
						{
							Logger.LogWarning("Sending a message on the hub timed-out.", exception: exception, metaData: metaData);
						}
						catch (Exception exception)
						{
							Logger.LogError("Sending a message on the hub resulted in an error.", exception: exception, metaData: metaData);
						}
					}, tokenSource.Token
				);

				tokenSource.CancelAfter(15 * 1000);
			}
			catch (Exception exception)
			{
				Logger.LogError("Queueing a message on the hub resulted in an error.", exception: exception, metaData: GetAdditionalDataForLogging(userToken));
			}
		}

		/// <summary>
		/// Send out an event to all users
		/// </summary>
		void INotificationHub.SendAllUsersEvent<TSingleSignOnToken>(IEvent<TSingleSignOnToken> eventData)
		{
			Logger.LogDebug("Sending a message on the hub to all users.");

			try
			{
				var tokenSource = new CancellationTokenSource();
				Guid correlationId = CorrelationIdHelper.GetCorrelationId();
				Task.Run
				(
					() =>
					{
						// Since we're crossing threads, we need to pass the correlationId
						try
						{
							CorrelationIdHelper.SetCorrelationId(correlationId);
						}
						catch (Exception exception)
						{
							Logger.LogWarning("Transferring CorrelationId across a thread failed.", exception: exception);
						}

						try
						{
							CurrentHub
								.Clients
								.All
								.notifyEvent(new { Type = eventData.GetType().FullName, Data = eventData });
						}
						catch (TimeoutException exception)
						{
							Logger.LogWarning("Sending a message on the hub to all users timed-out.", exception: exception);
						}
						catch (Exception exception)
						{
							Logger.LogError("Sending a message on the hub to all users resulted in an error.", exception: exception);
						}
					}, tokenSource.Token
				);

				tokenSource.CancelAfter(15 * 1000);
			}
			catch (Exception exception)
			{
				Logger.LogError("Queueing a message on the hub to all users resulted in an error.", exception: exception);
			}
		}

		/// <summary>
		/// Send out an event to all users except the specific user token
		/// </summary>
		void INotificationHub.SendExceptThisUserEvent<TSingleSignOnToken>(IEvent<TSingleSignOnToken> eventData, string userToken)
		{
			Logger.LogDebug(string.Format("Sending a message on the hub for all users except user [{0}].", userToken));

			return;

			try
			{
				var tokenSource = new CancellationTokenSource();
				Guid correlationId = CorrelationIdHelper.GetCorrelationId();
				Task.Run
				(
					() =>
					{
						var metaData = GetAdditionalDataForLogging(userToken);

						// Since we're crossing threads, we need to pass the correlationId
						try
						{
							CorrelationIdHelper.SetCorrelationId(correlationId);
						}
						catch (Exception exception)
						{
							Logger.LogWarning("Transferring CorrelationId across a thread failed.", exception: exception, metaData: metaData);
						}

						try
						{
							CurrentHub
								.Clients
								.Group(string.Format("User-{0}", userToken))
								.notifyEvent(new { Type = eventData.GetType().FullName, Data = eventData });
						}
						catch (TimeoutException exception)
						{
							Logger.LogWarning(string.Format("Sending a message on the hub for all users except user [{0}] timed-out.", userToken), exception: exception, metaData: metaData);
						}
						catch (Exception exception)
						{
							Logger.LogError(string.Format("Sending a message on the hub for all users except user [{0}] resulted in an error.", userToken), exception: exception, metaData: metaData);
						}
					}, tokenSource.Token
				);

				tokenSource.CancelAfter(15 * 1000);
			}
			catch (Exception exception)
			{
				Logger.LogError("Queueing a message on the hub resulted in an error.", exception: exception, metaData: GetAdditionalDataForLogging(userToken));
			}
		}

		protected virtual IDictionary<string, object> GetAdditionalDataForLogging(Guid userRsn)
		{
			return new Dictionary<string, object> { { "UserRsn", userRsn } };
		}

		protected virtual IDictionary<string, object> GetAdditionalDataForLogging(string userToken)
		{
			return new Dictionary<string, object> { { "UserToken", userToken } };
		}
	}
}