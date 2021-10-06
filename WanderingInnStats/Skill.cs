using System;
using System.Diagnostics;
using WanderingInnStats.Parsing;

namespace WanderingInnStats
{
    [DebuggerDisplay("{" + nameof(Name) + "}")]
    public class Skill : IEquatable<Skill>, IJsonMe
    {
        public Skill(string name, SkillType type)
        {
            Name = name;
            Type = type;
        }

        public string Name { get; }
        public SkillType Type { get; }

        public bool Equals(Skill? other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return Name == other.Name && Type == other.Type;
        }

        public override bool Equals(object? obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((Skill) obj);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Name, (int) Type);
        }

        public static bool operator ==(Skill? left, Skill? right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(Skill? left, Skill? right)
        {
            return !Equals(left, right);
        }

        public string JsonEquivalent => Name;
    }
}