﻿using System;
using System.Collections.Generic;
using System.Text;

namespace FreecraftCore.Payload.Serializer
{
	/// <summary>
	/// <see cref="ITypeSerializerStrategy"/> for Type <see cref="string"/>.
	/// </summary>
	public class stringSerializerStrategy : ITypeSerializerStrategy<string>
	{
		/// <summary>
        /// Perform the steps necessary to serialize the string.
        /// </summary>
        /// <param name="value">The string to be serialized.</param>
        /// <param name="dest">The writer entity that is accumulating the output data.</param>
		public void Write(string value, IWireMemberWriterStrategy dest)
		{
			//Review the source for Trinitycore's string reading for their ByteBuffer (payload/packet) Type.
			//(ctr+f << for std::string): http://www.trinitycore.net/d1/d17/ByteBuffer_8h_source.html
			//They use 0 byte to terminate the string in the stream
			
			//Convert the string to bytes
			//Not sure about encoding yet
			byte[] stringBytes = Encoding.ASCII.GetBytes(value);
			
			dest.Write(stringBytes);
			
			//Write the null terminator; Client expects it.
			dest.Write(0);
		}
		
		/// <summary>
        /// Perform the steps necessary to deserialize a string.
        /// </summary>
        /// <param name="source">The reader providing the input data.</param>
        /// <returns>A string value from the reader.</returns>
		public string Read(IWireMemberReaderStrategy source)
		{
			//Review the source for Trinitycore's string reading for their ByteBuffer (payload/packet) Type.
			//(ctr+f >> for std::string): http://www.trinitycore.net/d1/d17/ByteBuffer_8h_source.html
			//They use 0 byte to terminate the string in the stream
			
			//Read a byte from the stream; Stop when we find a 0
			List<byte> stringBytes = new List<byte>();
			
			byte currentByte = source.ReadByte();
			
			while(currentByte != 0)
			{
				stringBytes.Add(currentByte);
			}
			
			//Serializer design decision: Return null instead of String.Empty for no strings
			if(stringBytes.Count == 0)
				return null;
			else
				//Don't yet know the encoding we need
				return System.Text.Encoding.ASCII.GetString(stringBytes.ToArray()); //shouldn't need to reallocate array.
		}

		public stringSerializerStrategy()
		{
			//this serializer needs no subserializers or services.
		}
	}
}
