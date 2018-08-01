﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using NosCore.Configuration;
using NosCore.GameObject.Item;
using NosCore.Shared.Enumerations.Items;
using NosCore.Shared.I18N;

namespace NosCore.GameObject
{
    public class Inventory : ConcurrentDictionary<Guid, ItemInstance>
    {
        public WorldConfiguration Configuration { get; set; }
        public bool IsExpanded { get; set; }
        public long OwnerId { get; set; } //TODO remove

        //TODO remove
        public List<ItemInstance> CreateItem(short vnum, short amount = 1, PocketType? type = null, sbyte rare = 0, byte upgrade = 0, byte design = 0)
        {
            var newItem = ItemInstance.Create(vnum, OwnerId, amount, rare);
            newItem.Upgrade = upgrade;
            newItem.Design = design;
            if (newItem.Rare != 0 && newItem is WearableInstance wearable)
            {
                wearable.SetRarityPoint();
            }
            return AddItemToPocket(newItem, type);
        }

        public T LoadBySlotAndType<T>(short slot, PocketType type) where T : ItemInstance
        {
            T retItem = null;
            try
            {
                retItem = (T)this.Select(s => s.Value).SingleOrDefault(i => i != null && i.GetType() == typeof(T) && i.Slot == slot && i.Type == type);
            }
            catch (InvalidOperationException ioEx)
            {
                Logger.Error(ioEx);
                var isFirstItem = true;
                foreach (var item in this.Select(s => s.Value).Where(i => i != null && i.GetType() == typeof(T) && i.Slot == slot && i.Type == type))
                {
                    if (isFirstItem)
                    {
                        retItem = (T)item;
                        isFirstItem = false;
                        continue;
                    }
                    var iteminstance = this.Select(s => s.Value).FirstOrDefault(i => i != null && i.GetType() == typeof(T) && i.Slot == slot && i.Type == type);
                    if (iteminstance != null)
                    {
                        TryRemove(iteminstance.Id, out var value);
                    }
                }
            }
            return retItem;
        }

        //public bool CanAddItem(short itemVnum)
        //{
        //    var type = ServerManager.Instance.Items.Find(item => item.VNum == itemVnum).Type;
        //    return GetFreeSlot(type).HasValue;
        //}

        //public int CountItem(int itemVNum)
        //{
        //    return this.Select(s => s.Value).Where(s => s.ItemVNum == itemVNum).Sum(i => i.Amount);
        //}

        //public int CountItemInAnPocket(PocketType inv)
        //{
        //    return this.Count(s => s.Value.Type == inv);
        //}

        public List<ItemInstance> AddItemToPocket(ItemInstance newItem, PocketType? type = null)
        {
            var invlist = new List<ItemInstance>();

            // override type if necessary
            if (type.HasValue)
            {
                newItem.Type = type.Value;
            }

            // check if item can be stapled
            if (newItem.Type != PocketType.Bazaar && (newItem.Item.Type == PocketType.Etc || newItem.Item.Type == PocketType.Main))
            {
                var slotNotFull = this.ToList().Select(s => s.Value).Where(i => i.Type != PocketType.Bazaar && i.Type != PocketType.PetWarehouse && i.Type != PocketType.Warehouse && i.Type != PocketType.FamilyWareHouse && i.ItemVNum.Equals(newItem.ItemVNum) && i.Amount < Configuration.MaxItemAmount);
                var freeslot = Configuration.BackpackSize + (IsExpanded ? 1 : 0) * 12 - this.Count(s => s.Value.Type == newItem.Type);
                IEnumerable<ItemInstance> itemInstances = slotNotFull as IList<ItemInstance> ?? slotNotFull.ToList();
                if (newItem.Amount <= freeslot * Configuration.MaxItemAmount + itemInstances.Sum(s => Configuration.MaxItemAmount - s.Amount))
                {
                    foreach (var slot in itemInstances)
                    {
                        var max = slot.Amount + newItem.Amount;
                        max = max > Configuration.MaxItemAmount ? Configuration.MaxItemAmount : max;
                        newItem.Amount = (short)(slot.Amount + newItem.Amount - max);
                        newItem.Amount = newItem.Amount;
                        slot.Amount = (short)max;
                        invlist.Add(slot);
                    }
                }
            }
            if (newItem.Amount <= 0)
            {
                return invlist;
            }
            // create new item
            var freeSlot = newItem.Type == PocketType.Wear ? (LoadBySlotAndType<ItemInstance>((short)newItem.Item.EquipmentSlot, PocketType.Wear) == null
                    ? (short?)newItem.Item.EquipmentSlot
                    : null)
                : GetFreeSlot(newItem.Type);
            if (!freeSlot.HasValue)
            {
                return invlist;
            }
            var inv = AddToPocketWithSlotAndType(newItem, newItem.Type, freeSlot.Value);
            invlist.Add(inv);

            return invlist;
        }

        private short? GetFreeSlot(PocketType type)
        {
            var backPack = IsExpanded ? 1 : 0;
            var itemInstanceSlotsByType = this.Select(s => s.Value).Where(i => i.Type == type).OrderBy(i => i.Slot).Select(i => (int)i.Slot);
            IEnumerable<int> instanceSlotsByType = itemInstanceSlotsByType as int[] ?? itemInstanceSlotsByType.ToArray();
            var nextFreeSlot = instanceSlotsByType.Any()
                ? Enumerable.Range(0, (type != PocketType.Miniland ? Configuration.BackpackSize + (backPack * 12) : 50) + 1).Except(instanceSlotsByType).FirstOrDefault()
                : 0;
            return (short?)nextFreeSlot < (type != PocketType.Miniland ? Configuration.BackpackSize + (backPack * 12) : 50) ? (short?)nextFreeSlot : null;
        }
        private ItemInstance AddToPocketWithSlotAndType(ItemInstance itemInstance, PocketType type, short slot)
        {
            itemInstance.Slot = slot;
            itemInstance.Type = type;
            itemInstance.CharacterId = OwnerId;

            if (ContainsKey(itemInstance.Id))
            {
                Logger.Error(new InvalidOperationException("Cannot add the same ItemInstance twice to pocket."));
                return null;
            }

            if (this.Any(s => s.Value.Slot == slot && s.Value.Type == type))
            {
                return null;
            }
            if (itemInstance.Type == PocketType.Specialist && !(itemInstance is SpecialistInstance))
            {
                Logger.Error(new Exception("Cannot add an item of type Specialist without beeing a SpecialistInstance."));
            }
            if ((itemInstance.Type == PocketType.Equipment || itemInstance.Type == PocketType.Wear) && !(itemInstance is WearableInstance))
            {
                Logger.Error(new Exception("Cannot add an item of type Equipment or Wear without beeing a WearableInstance."));
            }
            this[itemInstance.Id] = itemInstance;
            return itemInstance;
        }

        //    public Tuple<short, PocketType> DeleteById(Guid id)
        //    {
        //        var inv = this[id];

        //        if (inv != null)
        //        {
        //            var removedPlace = new Tuple<short, PocketType>(inv.Slot, inv.Type);
        //            if (TryRemove(inv.Id, out var value))
        //            {
        //                return removedPlace;
        //            }
        //        }

        //        Logger.Error(new InvalidOperationException("Expected item wasn't deleted, Type or Slot did not match!"));
        //        return null;
        //    }

        //    public void DeleteFromSlotAndType(short slot, PocketType type)
        //    {
        //        var inv = this.Select(s => s.Value).FirstOrDefault(i => i.Slot.Equals(slot) && i.Type.Equals(type));

        //        if (inv != null)
        //        {
        //            TryRemove(inv.Id, out var value);
        //        }
        //        else
        //        {
        //            Logger.Error(new InvalidOperationException("Expected item wasn't deleted, Type or Slot did not match!"));
        //        }
        //    }

        //    public bool EnoughPlace(List<ItemInstance> itemInstances, int backPack)
        //    {
        //        var place = new Dictionary<PocketType, int>();
        //        foreach (var itemgroup in itemInstances.GroupBy(s => s.ItemVNum))
        //        {
        //            var type = itemgroup.First().Type;
        //            var listitem = this.Select(s => s.Value).Where(i => i.Type == type).ToList();
        //            if (!place.ContainsKey(type))
        //            {
        //                place.Add(type, (type != PocketType.Miniland ? Configuration.BackpackSize + backPack * 12 : 50) - listitem.Count);
        //            }

        //            var amount = itemgroup.Sum(s => s.Amount);
        //            var rest = amount % (type == PocketType.Equipment ? 1 : 99);
        //            var needanotherslot = listitem.Where(s => s.ItemVNum == itemgroup.Key).Sum(s => Configuration.MaxItemAmount - s.Amount) <= rest;
        //            place[type] -= (amount / (type == PocketType.Equipment ? 1 : 99)) + (needanotherslot ? 1 : 0);

        //            if (place[type] < 0)
        //            {
        //                return false;
        //            }
        //        }
        //        return true;
        //    }

        //    public T LoadByItemInstanceId<T>(Guid id) where T : ItemInstance
        //    {
        //        return (T)this[id];
        //    }

        //public ItemInstance LoadBySlotAndType(short slot, PocketType type)
        //{
        //    ItemInstance retItem = null;
        //    try
        //    {
        //        retItem = this.Select(s => s.Value).SingleOrDefault(i => i != null && i.Slot.Equals(slot) && i.Type.Equals(type));
        //    }
        //    catch (InvalidOperationException ioEx)
        //    {
        //        Logger.Error(ioEx);
        //        var isFirstItem = true;
        //        foreach (var item in this.Select(s => s.Value).Where(i => i != null && i.Slot.Equals(slot) && i.Type.Equals(type)))
        //        {
        //            if (isFirstItem)
        //            {
        //                retItem = item;
        //                isFirstItem = false;
        //                continue;
        //            }
        //            TryRemove(this.Select(s => s.Value).First(i => i != null && i.Slot.Equals(slot) && i.Type.Equals(type)).Id, out var value);
        //        }
        //    }
        //    return retItem;
        //}

        //    public ItemInstance MoveInPocket(short sourceSlot, PocketType sourceType, PocketType targetType, short? targetSlot = null, bool wear = true)
        //    {
        //        var sourceInstance = LoadBySlotAndType(sourceSlot, sourceType);

        //        if (sourceInstance == null && wear)
        //        {
        //            Logger.Error(new InvalidOperationException("SourceInstance to move does not exist."));
        //            return null;
        //        }
        //        if (sourceInstance != null)
        //        {
        //            if (targetSlot.HasValue)
        //            {
        //                if (wear)
        //                {
        //                    // swap
        //                    var targetInstance = LoadBySlotAndType(targetSlot.Value, targetType);

        //                    sourceInstance.Slot = targetSlot.Value;
        //                    sourceInstance.Type = targetType;

        //                    targetInstance.Slot = sourceSlot;
        //                    targetInstance.Type = sourceType;
        //                }
        //                else
        //                {
        //                    // move source to target
        //                    var freeTargetSlot = GetFreeSlot(targetType, IsExpanded ? 1 : 0);
        //                    if (!freeTargetSlot.HasValue)
        //                    {
        //                        return sourceInstance;
        //                    }
        //                    sourceInstance.Slot = freeTargetSlot.Value;
        //                    sourceInstance.Type = targetType;
        //                }

        //                return sourceInstance;
        //            }

        //            // check for free target slot
        //            short? nextFreeSlot;
        //            switch (targetType)
        //            {
        //                case PocketType.FirstPartner:
        //                case PocketType.SecondPartner:
        //                case PocketType.ThirdPartner:
        //                case PocketType.Wear:
        //                    nextFreeSlot = LoadBySlotAndType((short)sourceInstance.Item.EquipmentSlot, targetType) == null
        //                        ? (short)sourceInstance.Item.EquipmentSlot
        //                        : (short)-1;
        //                    break;

        //                default:
        //                    nextFreeSlot = GetFreeSlot(targetType, IsExpanded ? 1 : 0);
        //                    break;
        //            }
        //            if (nextFreeSlot.HasValue)
        //            {
        //                sourceInstance.Type = targetType;
        //                sourceInstance.Slot = nextFreeSlot.Value;
        //            }
        //            else
        //            {
        //                return null;
        //            }
        //        }
        //        else
        //        {
        //            return null;
        //        }
        //        return sourceInstance;
        //    }

        //    public void MoveItem(PocketType sourcetype, PocketType desttype, short sourceSlot, short amount, short destinationSlot, out ItemInstance sourcePocket, out ItemInstance destinationPocket)
        //    {
        //        // load source and destination slots
        //        sourcePocket = LoadBySlotAndType(sourceSlot, sourcetype);
        //        destinationPocket = LoadBySlotAndType(destinationSlot, desttype);
        //        if (sourcePocket != null && amount <= sourcePocket.Amount)
        //        {
        //            switch (destinationPocket)
        //            {
        //                case null when sourcePocket.Amount == amount:
        //                    sourcePocket.Slot = destinationSlot;
        //                    sourcePocket.Type = desttype;
        //                    break;
        //                case null:
        //                    ItemInstance itemDest = sourcePocket.Clone();
        //                    sourcePocket.Amount -= amount;
        //                    itemDest.Amount = amount;
        //                    itemDest.Type = desttype;
        //                    itemDest.Id = Guid.NewGuid();
        //                    AddToPocketWithSlotAndType(itemDest, desttype, destinationSlot);
        //                    break;
        //                default:
        //                    if (destinationPocket.ItemVNum == sourcePocket.ItemVNum && (byte)sourcePocket.Item.Type != 0)
        //                    {
        //                        if (destinationPocket.Amount + amount > Configuration.MaxItemAmount)
        //                        {
        //                            var saveItemCount = destinationPocket.Amount;
        //                            destinationPocket.Amount = Configuration.MaxItemAmount;
        //                            sourcePocket.Amount = (short)(saveItemCount + sourcePocket.Amount - Configuration.MaxItemAmount);
        //                        }
        //                        else
        //                        {
        //                            destinationPocket.Amount += amount;
        //                            sourcePocket.Amount -= amount;

        //                            // item with amount of 0 should be removed
        //                            if (sourcePocket.Amount == 0)
        //                            {
        //                                DeleteFromSlotAndType(sourcePocket.Slot, sourcePocket.Type);
        //                            }
        //                        }
        //                    }
        //                    else
        //                    {
        //                        // add and remove save pocket
        //                        destinationPocket = TakeItem(destinationPocket.Slot, destinationPocket.Type);
        //                        if (destinationPocket == null)
        //                        {
        //                            return;
        //                        }
        //                        destinationPocket.Slot = sourceSlot;
        //                        destinationPocket.Type = sourcetype;
        //                        sourcePocket = TakeItem(sourcePocket.Slot, sourcePocket.Type);
        //                        if (sourcePocket == null)
        //                        {
        //                            return;
        //                        }
        //                        sourcePocket.Slot = destinationSlot;
        //                        sourcePocket.Type = desttype;
        //                        this[destinationPocket.Id] = destinationPocket;
        //                        this[sourcePocket.Id] = sourcePocket;
        //                    }

        //                    break;
        //            }
        //        }
        //        sourcePocket = LoadBySlotAndType(sourceSlot, sourcetype);
        //        destinationPocket = LoadBySlotAndType(destinationSlot, desttype);
        //    }

        //    public IEnumerable<ItemInstance> RemoveItemAmount(int vnum, int amount = 1)
        //    {
        //        var remainingAmount = amount;

        //        foreach (var pocket in this.Select(s => s.Value).Where(s => s.ItemVNum == vnum && s.Type != PocketType.Wear && s.Type != PocketType.Bazaar && s.Type != PocketType.Warehouse && s.Type != PocketType.PetWarehouse && s.Type != PocketType.FamilyWareHouse).OrderBy(i => i.Slot))
        //        {
        //            if (remainingAmount > 0)
        //            {
        //                if (pocket.Amount > remainingAmount)
        //                {
        //                    // amount completely removed
        //                    pocket.Amount -= (short)remainingAmount;
        //                    remainingAmount = 0;
        //                }
        //                else
        //                {
        //                    // amount partly removed
        //                    remainingAmount -= pocket.Amount;
        //                    DeleteById(pocket.Id);
        //                }

        //                yield return pocket;
        //            }
        //            else
        //            {
        //                // amount to remove reached
        //                break;
        //            }
        //        }
        //    }

        //    public ItemInstance RemoveItemAmountFromPocket(short amount, Guid id)
        //    {

        //        var inv = this.Select(s => s.Value).FirstOrDefault(i => i.Id.Equals(id));

        //        if (inv == null)
        //        {
        //            return null;
        //        }
        //        inv.Amount -= amount;
        //        if (inv.Amount <= 0)
        //        {
        //            TryRemove(inv.Id, out _);
        //        }
        //        return inv;
        //    }

        //    public IEnumerable<ItemInstance> Reorder(ClientSession session, PocketType pocketType)
        //    {
        //        var itemsByPocketType = new List<ItemInstance>();
        //        switch (pocketType)
        //        {
        //            case PocketType.Costume:
        //                itemsByPocketType = this.Select(s => s.Value).Where(s => s.Type == PocketType.Costume).OrderBy(s => s.ItemVNum).ToList();
        //                break;

        //            case PocketType.Specialist:
        //                itemsByPocketType = this.Select(s => s.Value).Where(s => s.Type == PocketType.Specialist).OrderBy(s => s.Item.LevelJobMinimum).ToList();
        //                break;
        //        }

        //        short i = 0;

        //        foreach (var item in itemsByPocketType)
        //        {
        //            // remove item from pocket
        //            TryRemove(item.Id, out var value);
        //            yield return new ItemInstance(){Slot = item.Slot, Type = item.Type};
        //            // readd item to pocket
        //            item.Slot = i;
        //            yield return item;
        //            this[item.Id] = item;

        //            // increment slot
        //            i++;
        //        }
        //    }


        //private ItemInstance GetFreeSlot(IEnumerable<ItemInstance> slotfree)
        //{
        //    var pocketitemids = slotfree.Select(itemfree => itemfree.Id).ToList();
        //    return this.Select(s => s.Value).Where(i => pocketitemids.Contains(i.Id) && i.Type != PocketType.Wear && i.Type != PocketType.PetWarehouse && i.Type != PocketType.FamilyWareHouse && i.Type != PocketType.Warehouse && i.Type != PocketType.Bazaar).OrderBy(i => i.Slot).FirstOrDefault();
        //}


        //    private ItemInstance TakeItem(short slot, PocketType type)
        //    {
        //        var itemInstance = this.Select(s => s.Value).SingleOrDefault(i => i.Slot == slot && i.Type == type);
        //        if (itemInstance == null)
        //        {
        //            return null;
        //        }
        //        TryRemove(itemInstance.Id, out _);
        //        return itemInstance;
        //    }
    }
}
