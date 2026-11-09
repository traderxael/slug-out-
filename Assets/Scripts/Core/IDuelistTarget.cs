namespace SlugOut.Core
{
    public interface IDuelistTarget
    {
        bool IsPlayer { get; }
        ElementType DefenderElement { get; }
        void TakeDamage(float amount, ElementType attackerElement);
        void ApplyFreeze(float duration);
        void ApplyBurn(float damagePerSec, float duration);
        void ApplyStun(float duration);
    }
}
