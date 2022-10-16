namespace AnimalHusbandry
{
    public class Preset
    {
		public string Name = string.Empty;
		public float m_FoodCapacity;
		public float m_WaterCapacity;
		//public float m_TimeToCollapse;
		public float m_SleepTime;
		public float m_DecreaseFoodLevelPerSec;
		public float m_DecreaseWaterLevelPerSec;
		public float m_DecreasePoisonLevelPerSec;
		public float m_WaterLevelToDrink;
		public float m_FoodLevelToEat;
		public float m_DecreaseTrustPerSec;
		public float m_IncreaseTrustPerSec;
		public float m_TrustDecreaseOnHitMe;
		public float m_TrustDecreaseOnHitOther;
		public float m_OutsideFarmDecreaseTrustPerSec;
		public float m_TrustLevelToRunAway;
		public float m_PregnantCooldown;
		public float m_PregnantDuration;
		public float m_MaturationPerSec = 0.01f;
		//public float m_MaturityMinScale;
		public float m_DecreaseHealthPerSec;
		public float m_IncreaseHealthPerSec;
		public float m_MinFoodToGainTrust;
		public float m_MinWaterToGainTrust;
		public float m_NoTrustDistanceToPlayer;
		public float m_FollowWhistlerDuration;
		public float m_ShitInterval;
		//public string m_FarmTriggerIconName = string.Empty;
		//public string m_SleepingTriggerIconName = string.Empty;
		public float m_DurationOfBeingTied;
		public float m_PoisonFromShitPerSec;
		public int m_ShitPoisonLimit;
		public float m_MinTrustToWhistle;
		public float m_MinTrustToPet;
		//public List<GameObject> m_HarvestingResult0_50 = new List<GameObject>();
		//public List<GameObject> m_HarvestingResult50_100 = new List<GameObject>();
		public float m_MinTrustToSetName;
	}
}
