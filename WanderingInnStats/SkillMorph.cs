using System;
using System.Diagnostics;
using WanderingInnStats.Parsing;

namespace WanderingInnStats
{
    [DebuggerDisplay("{From}->{To}")]
    public class SkillMorph : IEquatable<SkillMorph>, IJsonMe
    {
        public string From { get; }
        public string To { get; }

        public SkillMorph(string @from, string to)
        {
            From = @from;
            To = to;
        }

        public bool Equals(SkillMorph? other)
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
            return Equals((SkillMorph) obj);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(From, To);
        }

        public static bool operator ==(SkillMorph? left, SkillMorph? right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(SkillMorph? left, SkillMorph? right)
        {
            return !Equals(left, right);
        }

        public string JsonEquivalent => $"{From}>{To}";
    }
}