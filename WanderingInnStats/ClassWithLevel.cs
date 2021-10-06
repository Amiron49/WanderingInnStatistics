using System;
using WanderingInnStats.Parsing;

namespace WanderingInnStats
{
    public class ClassWithLevel : IEquatable<ClassWithLevel>, IJsonMe
    {
        public string Name { get; }
        public int Level { get; }

        public ClassWithLevel(string name, int level)
        {
            Name = name;
            Level = level;
        }

        public bool Equals(ClassWithLevel? other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return Name == other.Name && Level == other.Level;
        }

        public override bool Equals(object? obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((ClassWithLevel) obj);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Name, Level);
        }

        public static bool operator ==(ClassWithLevel? left, ClassWithLevel? right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(ClassWithLevel? left, ClassWithLevel? right)
        {
            return !Equals(left, right);
        }

        public string JsonEquivalent => $"{Name}@{Level}";
    }
}