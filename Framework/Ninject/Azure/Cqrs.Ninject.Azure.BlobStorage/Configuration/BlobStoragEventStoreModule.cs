﻿#region Copyright
// // -----------------------------------------------------------------------
// // <copyright company="cdmdotnet Limited">
// // 	Copyright cdmdotnet Limited. All rights reserved.
// // </copyright>
// // -----------------------------------------------------------------------
#endregion

using Cqrs.Azure.BlobStorage;
using Cqrs.Azure.BlobStorage.Events;
using Cqrs.Events;
using Ninject.Modules;

namespace Cqrs.Ninject.Azure.BlobStorage.Configuration
{
	public class BlobStoragEventStoreModule<TAuthenticationToken> : NinjectModule
	{
		#region Overrides of NinjectModule

		/// <summary>
		/// Loads the module into the kernel.
		/// </summary>
		public override void Load()
		{
			RegisterFactories();
			RegisterServices();
			RegisterCqrsRequirements();
		}

		#endregion

		/// <summary>
		/// Register the all services
		/// </summary>
		public virtual void RegisterServices()
		{
		}

		/// <summary>
		/// Register the all factories
		/// </summary>
		public virtual void RegisterFactories()
		{
			Bind<IEventBuilder<TAuthenticationToken>>()
				.To<DefaultEventBuilder<TAuthenticationToken>>()
				.InSingletonScope();
			Bind<IEventDeserialiser<TAuthenticationToken>>()
				.To<EventDeserialiser<TAuthenticationToken>>()
				.InSingletonScope();
			Bind<IBlobStorageStoreConnectionStringFactory>()
				.To<BlobStorageEventStoreConnectionStringFactory>()
				.InSingletonScope();
		}

		/// <summary>
		/// Register the all Cqrs command handlers
		/// </summary>
		public virtual void RegisterCqrsRequirements()
		{
		}
	}
}