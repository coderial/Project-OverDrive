using UnityEngine;

namespace ProjectOverdrive.Controllers
{
    public interface IDamageable
    {
        void TakeDamage(float damage, Vector3 hitDirection, float knockback);
    }
}