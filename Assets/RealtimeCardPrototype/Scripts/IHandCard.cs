using System;
using System.Collections.Generic;
using UnityEngine;

namespace RealtimeCardPrototype
{
    public interface IHandCard
    {
        event Action<IHandCard> LeftHand;
        event Action<IHandCard> Played;
        Func<int, bool> TryPayMana { get; set; }
        int ManaCost { get; }
        bool IsDragging { get; }
        Transform Transform { get; }

        void ConfigureManaCost(int cost);
        void SetHandSlot(Vector3 position, Quaternion rotation);
        void SetPlayable(bool playable);
    }

    public interface IPowerBoostTarget : IHandCard
    {
        void AddPowerStack(int sourceId);
    }

}
