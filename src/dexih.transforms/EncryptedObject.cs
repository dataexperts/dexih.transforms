﻿using System;
using System.Runtime.Serialization;
using System.Text.Json.Serialization;



namespace dexih.transforms
{
    /// <summary>
    /// This is used to transport an encrypted value and the plaintext value through the transforms
    /// which allows the plaintext to be used for the delta comparison.
    /// </summary>
    public class EncryptedObject: IEquatable<string>, IEquatable<EncryptedObject>
    {
        [JsonIgnore, IgnoreDataMember]
        public object OriginalValue {get;}
        
        public string EncryptedValue {get;}
		
        public EncryptedObject(object originalValue, string encryptedValue)
        {
            OriginalValue = originalValue;
            EncryptedValue = encryptedValue;
        }
		
        public bool Equals(string value) 
        {
            return value == EncryptedValue;
        }
		
        public override string ToString()
        {
            return EncryptedValue;
        }

        public bool Equals(EncryptedObject other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return string.Equals(EncryptedValue, other.EncryptedValue);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((EncryptedObject) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((OriginalValue != null ? OriginalValue.GetHashCode() : 0) * 397) ^ (EncryptedValue != null ? EncryptedValue.GetHashCode() : 0);
            }
        }
    }
    
}