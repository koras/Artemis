using System;

namespace _Project.Scripts.Data.Offers
{
    [Flags]
    public enum OfferTriggerType
    {
        None = 0,
        Time = 1 << 0,
        ResourceEvent = 1 << 1,
        Manual = 1 << 2
    }
}
