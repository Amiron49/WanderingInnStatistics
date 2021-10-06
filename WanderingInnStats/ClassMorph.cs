using System;
using System.Diagnostics;
using WanderingInnStats.Parsing;

namespace WanderingInnStats
{
    [DebuggerDisplay("{From}->{To}")]
    public class ClassMorph : IEquatable<ClassMorph>, IJsonMe
    {
        public string From { get; }
        public string To { get; }

        public ClassMorph(string @from, string to)
        {
            From = @from;
            To = to;
        }

        public bool Equals(ClassMorph? other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return From == other.From && To == other.To;
        }

        public override bool Equals(object? obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((ClassMorph) obj);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(From, To);
        }

        public static bool operator ==(ClassMorph? left, ClassMorph? right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(ClassMorph? left, ClassMorph? right)
        {
            return !Equals(left, right);
        }

        public string JsonEquivalent => $"{From}>{To}";
    }
}