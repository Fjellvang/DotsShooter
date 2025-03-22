using DotsShooter.Common;
using Unity.Entities;
using Unity.Mathematics;

namespace DotsShooter.Weapons
{
    public static class WeaponDataExtensions
    {
         public static float3 CalculateNewDirectionBasedOnAccuracy(RefRW<WeaponData> weaponData, float3 direction, RefRW<EntityRandom> random)
         {
             if (weaponData.ValueRO.WeaponAccuracy >= 1) return direction; // if we are not 100% accurate, lets give it some spread.
             var accuracy = 1 - random.ValueRW.Value.NextFloat(weaponData.ValueRO.WeaponAccuracy, 1);
             var clockwise = random.ValueRW.Value.NextBool() ? 1 : -1;
             direction = Helpers.RotateVector2DTrig(direction, accuracy * 90 * clockwise);
             return direction;
         }
    }
}