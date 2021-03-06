﻿// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Spreads.Serialization;

namespace Spreads.DataTypes
{
    /// <summary>
    /// A blittable structure to store price values with decimal precision up to 15 digits.
    /// </summary>
    /// <remarks>
    ///  0                   1                   2                   3
    ///  0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1
    /// +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
    /// |S R R R|  -exp |        Int56 mantissa                         |
    /// +-------------------------------+-+-+---------------------------+
    /// |               Int56 mantissa                                  |
    /// +-------------------------------+-+-+---------------------------+
    /// S - sign
    /// R - reserved
    /// </remarks>
    [StructLayout(LayoutKind.Sequential, Pack = 8, Size = 8)]
    [Serialization(BlittableSize = 8)]
    public struct Price : IComparable<Price>, IEquatable<Price>, IConvertible
    {
        static Price()
        {
            if (!BitConverter.IsLittleEndian)
            {
                // Just in case. Should follow this pannern of failing fast when any method could depend
                // on endianess, then in version 42+ it will be easy to find all such occurencies
                Environment.FailFast("BigEndian is not supported");
            }
        }

        public static Price Zero = default(Price);

        /// <summary>
        /// 4-7 bits
        /// </summary>
        private const ulong ExponentMask = ((15UL << 56));

        private const ulong MantissaValueMask = ((1L << 55) - 1L);

        private readonly ulong _value;

        private static decimal[] DecimalFractions10 = new decimal[] {
            1M,
            0.1M,
            0.01M,
            0.001M,
            0.0001M,
            0.00001M,
            0.000001M,
            0.0000001M,
            0.00000001M,
            0.000000001M,
            0.0000000001M,
            0.00000000001M,
            0.000000000001M,
            0.0000000000001M,
            0.00000000000001M,
            0.000000000000001M,
        };

        private static double[] DoubleFractions10 = new double[] {
            1,
            0.1,
            0.01,
            0.001,
            0.0001,
            0.00001,
            0.000001,
            0.0000001,
            0.00000001,
            0.000000001,
            0.0000000001,
            0.00000000001,
            0.000000000001,
            0.0000000000001,
            0.00000000000001,
            0.000000000000001,
        };

        private static long[] Powers10 = new long[] {
            1,
            10,
            100,
            1000,
            10000,
            100000,
            1000000,
            10000000,
            100000000,
            1000000000,
            10000000000,
            100000000000,
            1000000000000,
            10000000000000,
            100000000000000,
            1000000000000000,
        };

        public int Exponent => (int)(((ulong)_value & ExponentMask) >> 56);

        public long Mantissa
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                var sign = (_value >> 55) & 1;
                var signMask = -(long)sign;
                var absValue = _value & MantissaValueMask;
                var mantissaValue = (absValue - sign) ^ (ulong)signMask;
                return (long)mantissaValue;
            }
        }

        public decimal AsDecimal => (this);
        public double AsDouble => (double)(this);

        public Price(int exponent, long mantissaValue)
        {
            if ((ulong)exponent > 15) throw new ArgumentOutOfRangeException(nameof(exponent));

            var signMask = mantissaValue >> 63;
            var sign = (signMask & 1);
            var absValue = (mantissaValue ^ signMask) + sign;

            // enough bits
            if (((ulong)absValue & ~MantissaValueMask) != 0UL) throw new ArgumentOutOfRangeException(nameof(mantissaValue));

            var mantissaPart = absValue | (sign << 55);

            // now mantissa is checked to have 0-7th bits from left as zero, just write exponent to it
            var exponentPart = ((ulong)exponent << 56) & ExponentMask;

            _value = (ulong)mantissaPart | exponentPart;
        }

        public Price(decimal value, int precision = 5)
        {
            if ((ulong)precision > 15) throw new ArgumentOutOfRangeException(nameof(precision));
            var mantissaValue = decimal.ToInt64(value * Powers10[precision]);

            var signMask = mantissaValue >> 63;
            var sign = (signMask & 1);
            var absValue = (mantissaValue ^ signMask) + sign;

            // enough bits
            if (((ulong)absValue & ~MantissaValueMask) != 0UL) throw new ArgumentOutOfRangeException(nameof(mantissaValue));

            var mantissaPart = absValue | (sign << 55);
            var exponentPart = ((ulong)precision << 56) & ExponentMask;

            _value = (ulong)mantissaPart | exponentPart;
        }

        public Price(double value, int precision = 5)
        {
            if ((ulong)precision > 15) throw new ArgumentOutOfRangeException(nameof(precision));
            var mantissaValue = checked((long)(value * Powers10[precision]));

            var signMask = mantissaValue >> 63;
            var sign = (signMask & 1);
            var absValue = (mantissaValue ^ signMask) + sign;

            // enough bits
            if (((ulong)absValue & ~MantissaValueMask) != 0UL) throw new ArgumentOutOfRangeException(nameof(mantissaValue));

            var mantissaPart = absValue | (sign << 55);
            var exponentPart = ((ulong)precision << 56) & ExponentMask;

            _value = (ulong)mantissaPart | exponentPart;
        }

        // NB only decimal is implicit because it doesn't lose precision
        // there are no conversions to other direction, only ctor

        public static explicit operator double(Price price)
        {
            return price.Mantissa * DoubleFractions10[price.Exponent];
        }

        public static explicit operator float(Price price)
        {
            return (float)(price.Mantissa * DoubleFractions10[price.Exponent]);
        }

        public static implicit operator decimal(Price price)
        {
            return price.Mantissa * DecimalFractions10[price.Exponent];
        }

        public static implicit operator Price(int value)
        {
            return new Price(0, (long)value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int CompareTo(Price other)
        {
            var c = (int)this.Exponent - (int)other.Exponent;
            if (c == 0)
            {
                return this.Mantissa.CompareTo(other.Mantissa);
            }
            if (c > 0)
            {
                return (this.Mantissa * Powers10[c]).CompareTo(other.Mantissa);
            }
            else
            {
                return this.Mantissa.CompareTo(other.Mantissa * Powers10[-c]);
            }
        }

        public bool Equals(Price other)
        {
            if (_value == other._value)
            {
                return true;
            }
            var c = Exponent - other.Exponent;
            if (c == 0)
            {
                // NB if exponents are equal, then equality is possible only if mantissas are equal,
                // but we have covered this case in _value comparison, therefore return just false
                return false;
            }
            if (c > 0)
            {
                return Mantissa * Powers10[c] == other.Mantissa;
            }
            else
            {
                return Mantissa == other.Mantissa * Powers10[-c];
            }
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            return obj is Price && Equals((Price)obj);
        }

        public static bool operator ==(Price x, Price y)
        {
            return x.Equals(y);
        }

        public static bool operator !=(Price x, Price y)
        {
            return !x.Equals(y);
        }

        public static bool operator >(Price x, Price y)
        {
            return x.CompareTo(y) > 0;
        }

        public static bool operator <(Price x, Price y)
        {
            return x.CompareTo(y) < 0;
        }

        public static bool operator >=(Price x, Price y)
        {
            return x.CompareTo(y) >= 0;
        }

        public static bool operator <=(Price x, Price y)
        {
            return x.CompareTo(y) <= 0;
        }

        public static Price operator -(Price x)
        {
            var newPrice = new Price(x.Exponent, -x.Mantissa);
            return newPrice;
        }

        public static Price operator +(Price x, Price y)
        {
            if (x.Exponent == y.Exponent)
            {
                return new Price((int)x.Exponent, (long)(x.Mantissa + y.Mantissa));
            }
            return new Price((decimal)x + (decimal)y, (int)Math.Max(x.Exponent, y.Exponent));
        }

        public static Price operator -(Price x, Price y)
        {
            if (x.Exponent == y.Exponent)
            {
                return new Price((int)x.Exponent, (long)(x.Mantissa - y.Mantissa));
            }
            return new Price((decimal)x - (decimal)y, (int)Math.Max(x.Exponent, y.Exponent));
        }

        public static Price operator *(Price x, int y)
        {
            return new Price((int)x.Exponent, (long)(x.Mantissa * y));
        }

        public override string ToString()
        {
            var asDecimal = (decimal)this;
            return asDecimal.ToString(CultureInfo.InvariantCulture);
        }

        public override int GetHashCode()
        {
            return (int)(_value & int.MaxValue);
        }

        #region IConvertible

        public TypeCode GetTypeCode()
        {
            return TypeCode.Decimal;
        }

        public bool ToBoolean(IFormatProvider provider)
        {
            throw new InvalidCastException();
        }

        public char ToChar(IFormatProvider provider)
        {
            throw new InvalidCastException();
        }

        public sbyte ToSByte(IFormatProvider provider)
        {
            throw new InvalidCastException();
        }

        public byte ToByte(IFormatProvider provider)
        {
            throw new InvalidCastException();
        }

        public short ToInt16(IFormatProvider provider)
        {
            throw new InvalidCastException();
        }

        public ushort ToUInt16(IFormatProvider provider)
        {
            throw new InvalidCastException();
        }

        public int ToInt32(IFormatProvider provider)
        {
            throw new InvalidCastException();
        }

        public uint ToUInt32(IFormatProvider provider)
        {
            throw new InvalidCastException();
        }

        public long ToInt64(IFormatProvider provider)
        {
            throw new InvalidCastException();
        }

        public ulong ToUInt64(IFormatProvider provider)
        {
            throw new InvalidCastException();
        }

        public float ToSingle(IFormatProvider provider)
        {
            return (float)this;
        }

        public double ToDouble(IFormatProvider provider)
        {
            return (double)this;
        }

        public decimal ToDecimal(IFormatProvider provider)
        {
            return (decimal)this;
        }

        public DateTime ToDateTime(IFormatProvider provider)
        {
            throw new InvalidCastException();
        }

        public string ToString(IFormatProvider provider)
        {
            return this.ToString();
        }

        public object ToType(Type conversionType, IFormatProvider provider)
        {
            throw new InvalidCastException();
        }

        #endregion IConvertible
    }

    public class InvalidPriceException : Exception
    {
        public InvalidPriceException(string message) : base(message)
        {
        }
    }
}