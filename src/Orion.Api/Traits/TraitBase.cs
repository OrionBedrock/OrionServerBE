using System.Reflection;

namespace Orion.Api.Traits;

/// <summary>Shared trait lifecycle without Protocol dependencies.</summary>
public abstract class TraitBase
{
    private uint _randomTickNumerator = 1;
    private uint _randomTickDenominator = 4096;

    public virtual string Identifier
    {
        get
        {
            if (GetType().GetProperty("Identifier", BindingFlags.Public | BindingFlags.Static) is PropertyInfo property &&
                property.PropertyType == typeof(string) &&
                property.GetValue(null) is string identifier &&
                !string.IsNullOrWhiteSpace(identifier))
            {
                return identifier;
            }

            return GetType().FullName ?? GetType().Name;
        }
    }

    public virtual void OnAdd()
    {
    }

    public virtual void OnRemove()
    {
    }

    public virtual void OnTick(TraitOnTickDetails details)
    {
    }

    public virtual void OnRandomTick()
    {
    }

    public bool ShouldRandomTick(uint factor = 1)
    {
        if (_randomTickNumerator == 0)
        {
            return false;
        }

        if (_randomTickNumerator == _randomTickDenominator)
        {
            return true;
        }

        double chance = (double)(_randomTickNumerator * factor) / _randomTickDenominator;
        return Random.Shared.NextDouble() < chance;
    }

    public void SetRandomTickProbability(uint numerator, uint denominator)
    {
        if (denominator == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(denominator), "Denominator must be greater than 0.");
        }

        if (numerator > denominator)
        {
            throw new ArgumentOutOfRangeException(nameof(numerator), "Numerator must be less than or equal to denominator.");
        }

        _randomTickNumerator = numerator;
        _randomTickDenominator = denominator;
    }
}
