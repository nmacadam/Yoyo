// Yoyo Network Engine, 2021
// Author: Nathan MacAdam

using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Yoyo.Runtime
{
	public enum PacketType
	{
		Empty = 0x0,
		PlayerId = 0x1,
		Create = 0x2,
		Destroy = 0x3,
		Dirty = 0x4,
		Disconnect = 0x5,
		Command = 0x6,
		Update = 0x7,
	}
	
	public struct PacketHeader
	{
		public uint Protocol;
        public uint Sequence;
        public uint PacketType;
        public uint PacketBufferLength;
	}

	public partial class Packet
	{
		private List<byte> _buffer;
        private byte[] _readableBuffer;
        private int _index;

		public Packet()
        {
            _buffer = new List<byte>();
            _index = 0;
        }

        public Packet(uint sequence, uint packetType)
        {
            _buffer = new List<byte>();
            _index = 0;

            // Write header
            WriteHeader(sequence, packetType);
        }

        public Packet(byte[] buffer)
        {
            _buffer = new List<byte>();
            _index = 0;

            SetBytes(buffer);
        }

        public Packet(Packet packet)
        {
            _buffer = new List<byte>();
            SetBytes(packet.ToArray());
            _index = packet._index;
        }

        public void WriteHeader(uint sequence, uint packetType)
        {
            Write(YoyoVersionInfo.ProtocolId);
            Write(sequence);
            Write(packetType);
        }

        public PacketHeader ReadHeader(bool moveReadPosition = true)
        {
            if (_buffer.Count > _index)
            {
                PacketHeader header = new PacketHeader()
                {
                    Protocol = ReadUInt(moveReadPosition),
                    Sequence = ReadUInt(moveReadPosition),
                    PacketType = ReadUInt(moveReadPosition)
                };
                return header;
            }
            else
            {
                throw new Exception("Could not read header!");
            }
        }

        public int ReadLength(bool moveReadPosition = true)
        {
            if (_buffer.Count > _index)
            {
                return ReadInt(moveReadPosition);
            }
            else
            {
                throw new Exception("Could not read length!");
            }
        }

        #region Buffer Manipulation

        /// <summary>Sets the packet's content and prepares it to be read.</summary>
        /// <param name="_data">The bytes to add to the packet.</param>
        public void SetBytes(byte[] data)
        {
            Write(data);
            _readableBuffer = _buffer.ToArray();
        }

        /// <summary>Inserts the length of the packet's content at the start of the buffer.</summary>
        public void WriteLength()
        {
            _buffer.InsertRange(0, BitConverter.GetBytes(_buffer.Count)); // Insert the byte length of the packet at the very beginning
        }

        /// <summary>Inserts the given int at the start of the buffer.</summary>
        /// <param name="value">The int to insert.</param>
        public void InsertInt(int value)
        {
            _buffer.InsertRange(0, BitConverter.GetBytes(value)); // Insert the int at the start of the buffer
        }

        /// <summary>Gets the packet's content in array form.</summary>
        public byte[] ToArray()
        {
            _readableBuffer = _buffer.ToArray();
            return _readableBuffer;
        }

        /// <summary>Gets the length of the packet's content.</summary>
        public int Length()
        {
            return _buffer.Count; // Return the length of buffer
        }

        /// <summary>Gets the length of the unread data contained in the packet.</summary>
        public int UnreadLength()
        {
            return Length() - _index; // Return the remaining length (unread)
        }

        /// <summary>Resets the packet instance to allow it to be reused.</summary>
        /// <param name="shouldReset">Whether or not to reset the packet.</param>
        public void Reset(bool shouldReset = true)
        {
            if (shouldReset)
            {
                _buffer.Clear(); // Clear buffer
                _readableBuffer = null;
                _index = 0; // Reset readPos
            }
            else
            {
                _index -= 4; // "Unread" the last read int
            }
        }

        #region Writing

        /// <summary>Adds a byte to the packet.</summary>
        /// <param name="value">The byte to add.</param>
        public void Write(byte value)
        {
            _buffer.Add(value);
        }

        /// <summary>Adds an array of bytes to the packet.</summary>
        /// <param name="value">The byte array to add.</param>
        public void Write(byte[] value)
        {
            _buffer.AddRange(value);
        }

        /// <summary>Adds a short to the packet.</summary>
        /// <param name="value">The short to add.</param>
        public void Write(short value)
        {
            _buffer.AddRange(BitConverter.GetBytes(value));
        }

        /// <summary>Adds an uint to the packet.</summary>
        /// <param name="value">The uint to add.</param>
        public void Write(uint value)
        {
            _buffer.AddRange(BitConverter.GetBytes(value));
        }

        /// <summary>Adds an int to the packet.</summary>
        /// <param name="value">The int to add.</param>
        public void Write(int value)
        {
            _buffer.AddRange(BitConverter.GetBytes(value));
        }

        /// <summary>Adds a long to the packet.</summary>
        /// <param name="value">The long to add.</param>
        public void Write(long value)
        {
            _buffer.AddRange(BitConverter.GetBytes(value));
        }

        /// <summary>Adds a float to the packet.</summary>
        /// <param name="value">The float to add.</param>
        public void Write(float value)
        {
            _buffer.AddRange(BitConverter.GetBytes(value));
        }

        /// <summary>Adds a double to the packet.</summary>
        /// <param name="value">The double to add.</param>
        public void Write(double value)
        {
            _buffer.AddRange(BitConverter.GetBytes(value));
        }

        /// <summary>Adds a bool to the packet.</summary>
        /// <param name="value">The bool to add.</param>
        public void Write(bool value)
        {
            _buffer.AddRange(BitConverter.GetBytes(value));
        }

        /// <summary>Adds a string to the packet.</summary>
        /// <param name="value">The string to add.</param>
        public void Write(string value)
        {
            Write(value.Length); // Add the length of the string to the packet
            _buffer.AddRange(Encoding.ASCII.GetBytes(value)); // Add the string itself
        }

        /// <summary>Adds a Vector2 to the packet.</summary>
        /// <param name="value">The Vector2 to add.</param>
        public void Write(Vector2 value)
        {
            Write(value.x);
            Write(value.y);
        }

        /// <summary>Adds a Vector3 to the packet.</summary>
        /// <param name="value">The Vector3 to add.</param>
        public void Write(Vector3 value)
        {
            Write(value.x);
            Write(value.y);
            Write(value.z);
        }

        /// <summary>Adds a Vector4 to the packet.</summary>
        /// <param name="value">The Vector4 to add.</param>
        public void Write(Vector4 value)
        {
            Write(value.x);
            Write(value.y);
            Write(value.z);
            Write(value.w);
        }

        /// <summary>Adds a Quaternion to the packet.</summary>
        /// <param name="value">The Quaternion to add.</param>
        public void Write(Quaternion value)
        {
            Write(value.x);
            Write(value.y);
            Write(value.z);
            Write(value.w);
        }

        /// <summary>Adds a Color to the packet.</summary>
        /// <param name="value">The Color to add.</param>
        public void Write(Color value)
        {
            Write(value.r);
            Write(value.g);
            Write(value.b);
            Write(value.a);
        }

        #endregion

        #region Reading

        /// <summary>Reads a byte from the packet.</summary>
        /// <param name="moveReadPosition">Whether or not to move the buffer's read position.</param>
        public byte ReadByte(bool moveReadPosition = true)
        {
            if (_buffer.Count > _index)
            {
                // If there are unread bytes
                byte value = _readableBuffer[_index]; // Get the byte at readPos' position
                if (moveReadPosition)
                {
                    // If moveReadPosition is true
                    _index += 1; // Increase readPos by 1
                }
                return value; // Return the byte
            }
            else
            {
                throw new Exception("Could not read value of type 'byte'!");
            }
        }

        /// <summary>Reads an array of bytes from the packet.</summary>
        /// <param name="length">The length of the byte array.</param>
        /// <param name="moveReadPosition">Whether or not to move the buffer's read position.</param>
        public byte[] ReadBytes(int length, bool moveReadPosition = true)
        {
            if (_buffer.Count > _index)
            {
                // If there are unread bytes
                byte[] value = _buffer.GetRange(_index, length).ToArray(); // Get the bytes at readPos' position with a range of _length
                if (moveReadPosition)
                {
                    // If moveReadPosition is true
                    _index += length; // Increase readPos by _length
                }
                return value; // Return the bytes
            }
            else
            {
                throw new Exception("Could not read value of type 'byte[]'!");
            }
        }

        /// <summary>Reads a short from the packet.</summary>
        /// <param name="moveReadPosition">Whether or not to move the buffer's read position.</param>
        public short ReadShort(bool moveReadPosition = true)
        {
            if (_buffer.Count > _index)
            {
                // If there are unread bytes
                short value = BitConverter.ToInt16(_readableBuffer, _index); // Convert the bytes to a short
                if (moveReadPosition)
                {
                    // If moveReadPosition is true and there are unread bytes
                    _index += 2; // Increase readPos by 2
                }
                return value; // Return the short
            }
            else
            {
                throw new Exception("Could not read value of type 'short'!");
            }
        }

        /// <summary>Reads an uint from the packet.</summary>
        /// <param name="moveReadPosition">Whether or not to move the buffer's read position.</param>
        public uint ReadUInt(bool moveReadPosition = true)
        {
            if (_buffer.Count > _index)
            {
                // If there are unread bytes
                uint value = BitConverter.ToUInt32(_readableBuffer, _index); // Convert the bytes to an uint
                if (moveReadPosition)
                {
                    // If moveReadPosition is true
                    _index += 4; // Increase readPos by 4
                }
                return value; // Return the uint
            }
            else
            {
                throw new Exception("Could not read value of type 'int'!");
            }
        }

        /// <summary>Reads an int from the packet.</summary>
        /// <param name="moveReadPosition">Whether or not to move the buffer's read position.</param>
        public int ReadInt(bool moveReadPosition = true)
        {
            if (_buffer.Count > _index)
            {
                // If there are unread bytes
                int value = BitConverter.ToInt32(_readableBuffer, _index); // Convert the bytes to an int
                if (moveReadPosition)
                {
                    // If moveReadPosition is true
                    _index += 4; // Increase readPos by 4
                }
                return value; // Return the int
            }
            else
            {
                throw new Exception("Could not read value of type 'int'!");
            }
        }

        /// <summary>Reads a long from the packet.</summary>
        /// <param name="moveReadPosition">Whether or not to move the buffer's read position.</param>
        public long ReadLong(bool moveReadPosition = true)
        {
            if (_buffer.Count > _index)
            {
                // If there are unread bytes
                long value = BitConverter.ToInt64(_readableBuffer, _index); // Convert the bytes to a long
                if (moveReadPosition)
                {
                    // If moveReadPosition is true
                    _index += 8; // Increase readPos by 8
                }
                return value; // Return the long
            }
            else
            {
                throw new Exception("Could not read value of type 'long'!");
            }
        }

        /// <summary>Reads a float from the packet.</summary>
        /// <param name="moveReadPosition">Whether or not to move the buffer's read position.</param>
        public float ReadFloat(bool moveReadPosition = true)
        {
            if (_buffer.Count > _index)
            {
                // If there are unread bytes
                float value = BitConverter.ToSingle(_readableBuffer, _index); // Convert the bytes to a float
                if (moveReadPosition)
                {
                    // If moveReadPosition is true
                    _index += 4; // Increase readPos by 4
                }
                return value; // Return the float
            }
            else
            {
                throw new Exception("Could not read value of type 'float'!");
            }
        }

        /// <summary>Reads a double from the packet.</summary>
        /// <param name="moveReadPosition">Whether or not to move the buffer's read position.</param>
        public double ReadDouble(bool moveReadPosition = true)
        {
            if (_buffer.Count > _index)
            {
                // If there are unread bytes
                double value = BitConverter.ToDouble(_readableBuffer, _index); // Convert the bytes to a float
                if (moveReadPosition)
                {
                    // If moveReadPosition is true
                    _index += 8; // Increase readPos by 4
                }
                return value; // Return the float
            }
            else
            {
                throw new Exception("Could not read value of type 'float'!");
            }
        }

        /// <summary>Reads a bool from the packet.</summary>
        /// <param name="moveReadPosition">Whether or not to move the buffer's read position.</param>
        public bool ReadBool(bool moveReadPosition = true)
        {
            if (_buffer.Count > _index)
            {
                // If there are unread bytes
                bool value = BitConverter.ToBoolean(_readableBuffer, _index); // Convert the bytes to a bool
                if (moveReadPosition)
                {
                    // If moveReadPosition is true
                    _index += 1; // Increase readPos by 1
                }
                return value; // Return the bool
            }
            else
            {
                throw new Exception("Could not read value of type 'bool'!");
            }
        }

        /// <summary>Reads a string from the packet.</summary>
        /// <param name="moveReadPosition">Whether or not to move the buffer's read position.</param>
        public string ReadString(bool moveReadPosition = true)
        {
            try
            {
                int _length = ReadInt(); // Get the length of the string
                string value = Encoding.ASCII.GetString(_readableBuffer, _index, _length); // Convert the bytes to a string
                if (moveReadPosition && value.Length > 0)
                {
                    // If moveReadPosition is true string is not empty
                    _index += _length; // Increase readPos by the length of the string
                }
                return value; // Return the string
            }
            catch
            {
                throw new Exception("Could not read value of type 'string'!");
            }
        }

        public Vector2 ReadVector2(bool moveReadPos = true)
        {
            return new Vector2(ReadFloat(moveReadPos), ReadFloat(moveReadPos));
        }

        public Vector3 ReadVector3(bool moveReadPos = true)
        {
            return new Vector3(ReadFloat(moveReadPos), ReadFloat(moveReadPos), ReadFloat(moveReadPos));
        }

        public Vector4 ReadVector4(bool moveReadPos = true)
        {
            return new Vector4(ReadFloat(moveReadPos), ReadFloat(moveReadPos), ReadFloat(moveReadPos), ReadFloat(moveReadPos));
        }

        public Quaternion ReadQuaternion(bool moveReadPos = true)
        {
            return new Quaternion(ReadFloat(moveReadPos), ReadFloat(moveReadPos), ReadFloat(moveReadPos), ReadFloat(moveReadPos));
        }

        public Color ReadColor(bool moveReadPos = true)
        {
            return new Color(ReadFloat(moveReadPos), ReadFloat(moveReadPos), ReadFloat(moveReadPos), ReadFloat(moveReadPos));
        }

        #endregion
        #endregion
	}
}