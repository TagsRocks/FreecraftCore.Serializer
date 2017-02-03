﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using System.Reflection;
using Fasterflect;
using JetBrains.Annotations;

namespace FreecraftCore.Serializer.KnownTypes
{
	public class SubComplexTypeSerializerDecorator<TBaseType> : ITypeSerializerStrategy<TBaseType>
	{
		/// <inheritdoc />
		public Type SerializerType { get; } = typeof(TBaseType);

		/// <summary>
		/// General serializer provider service.
		/// </summary>
		[NotNull]
		private IGeneralSerializerProvider serializerProviderService { get; }

		/// <summary>
		/// The lookup table that maps ints to the Type.
		/// </summary>
		[NotNull]
		private IDictionary<int, Type> keyToTypeLookup { get; }

		/// <summary>
		/// The lookup table that maps types to an int.
		/// </summary>
		[NotNull]
		private IDictionary<Type, int> typeToKeyLookup { get; }


		/// <inheritdoc />
		public SerializationContextRequirement ContextRequirement { get; } = SerializationContextRequirement.Contextless;

		/// <summary>
		/// Provides read and write stragey for child keys.
		/// </summary>
		[NotNull]
		private IChildKeyStrategy keyStrategy { get; }

		//TODO: Reimplement default type handling cleanly
		[CanBeNull]
		public ITypeSerializerStrategy DefaultSerializer { get; }

		public SubComplexTypeSerializerDecorator(IGeneralSerializerProvider serializerProvider, IChildKeyStrategy childKeyStrategy)
		{
			if (serializerProvider == null)
				throw new ArgumentNullException(nameof(serializerProvider), $"Provided {nameof(serializerProvider)} service was null.");

			if (childKeyStrategy == null)
				throw new ArgumentNullException(nameof(childKeyStrategy), $"Provided {nameof(IChildKeyStrategy)} used for key read and write is null.");

			keyStrategy = childKeyStrategy;
			serializerProviderService = serializerProvider;
			typeToKeyLookup = new Dictionary<Type, int>();
			keyToTypeLookup = new Dictionary<int, Type>();

			DefaultSerializer = typeof(TBaseType).Attribute<DefaultChildAttribute>() != null
				? serializerProviderService.Get(typeof(TBaseType).Attribute<DefaultChildAttribute>().ChildType) : null;

			//We no longer reserve 0. Sometimes type information of a child is sent as a 0 in WoW protocol. We can opt for mostly metadata marker style interfaces.
			//TODO: Add support for basetype serialization metadata marking.
			foreach (WireDataContractBaseTypeAttribute wa in typeof(TBaseType).Attributes<WireDataContractBaseTypeAttribute>())
			{
				try
				{
					keyToTypeLookup.Add(wa.Index, wa.ChildType);
					typeToKeyLookup.Add(wa.ChildType, wa.Index);
				}
				catch(ArgumentException e)
				{
					throw new InvalidOperationException($"Failed to register child Type: {wa.ChildType} for BaseType: {typeof(TBaseType).FullName} due to likely duplicate key index for {wa.Index}. Index must be unique per Type.", e);
				}
			}
		}

		/// <inheritdoc />
		public void Write(TBaseType value, IWireMemberWriterStrategy dest)
		{
			if (dest == null) throw new ArgumentNullException(nameof(dest));

			//TODO: Clean up default serializer implementation
			if (!typeToKeyLookup.ContainsKey(value.GetType()))
			{
				throw new InvalidOperationException($"{this.GetType()} attempted to serialize a child Type: {value.GetType()} but no valid type matches. Writing cannot use default types.");
			}

			//TODO: Oh man, this is a disaster. How do we handle the default? How do we tell consumers to use the default?
			//Defer key writing to the key writing strategy
			keyStrategy.Write(typeToKeyLookup[value.GetType()], dest);

			ITypeSerializerStrategy serializer;

			try
			{
				serializer = serializerProviderService.Get(value.GetType());

			}
			catch (KeyNotFoundException e)
			{
				throw new InvalidOperationException($"Couldn't locate serializer for {value.GetType().FullName} in the {nameof(IGeneralSerializerProvider)} service.", e);
			}

			serializer.Write(value, dest);
		}

		/// <inheritdoc />
		public TBaseType Read(IWireMemberReaderStrategy source)
		{
			if (source == null) throw new ArgumentNullException(nameof(source));

			//Incoming should be a byte that indicates the child type to use
			//Read it to lookup in the map to determine which type we should create
			int childIndexRequested = keyStrategy.Read(source); //defer to key reader (could be int, byte or something else)

			//Check if we have that index
			if (!keyToTypeLookup.ContainsKey(childIndexRequested))
			{
				if (DefaultSerializer != null)
				{
					return (TBaseType)DefaultSerializer.Read(source);
				}
				else
					throw new InvalidOperationException($"{this.GetType()} attempted to deserialize a child of Type: {typeof(TBaseType).FullName} with Key: {childIndexRequested} but no valid type matches and there is no default type.");

			}

			Type childTypeRequest = keyToTypeLookup[childIndexRequested];

			if(childTypeRequest == null)
				throw new InvalidOperationException($"{this.GetType()} attempted to deserialize to a child type with Index: {childIndexRequested} but the lookup table provided a null type. This may indicate a failure in registeration of child types.");

			//TODO: Handle exception
			return (TBaseType)serializerProviderService.Get(childTypeRequest).Read(source);
		}

		/// <inheritdoc />
		void ITypeSerializerStrategy.Write(object value, IWireMemberWriterStrategy dest)
		{
			if (dest == null) throw new ArgumentNullException(nameof(dest));

			Write((TBaseType)value, dest);
		}

		/// <inheritdoc />
		object ITypeSerializerStrategy.Read(IWireMemberReaderStrategy source)
		{
			if (source == null) throw new ArgumentNullException(nameof(source));

			return Read(source);
		}
	}
}
