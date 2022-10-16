using UnityEngine;

namespace AnimalHusbandry
{
    public class PlayerExtended : Player
    {
        protected override void Start()
        {
            base.Start();
            new GameObject("__AnimalHusbandryMod__").AddComponent<AnimalHusbandry>();
        }
    }
}
