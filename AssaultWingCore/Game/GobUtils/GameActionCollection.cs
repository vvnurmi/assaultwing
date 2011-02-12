﻿using System.Collections.Generic;
using System.Linq;
using AW2.Helpers.Serialization;

namespace AW2.Game.GobUtils
{
    public class GameActionCollection : IEnumerable<GameAction>, INetworkSerializable
    {
        private List<GameAction> _items;
        private List<GameAction> _toRemove;
        private Player _owner;

        public GameActionCollection(Player owner)
        {
            _items = new List<GameAction>();
            _toRemove = new List<GameAction>();
            _owner = owner;
        }

        public void AddOrReplace(GameAction item)
        {
            int index = _items.FindIndex(a => a.GetType() == item.GetType());
            if (index >= 0)
                _items[index] = item;
            else
                _items.Add(item);
        }

        public void RemoveLater(GameAction item)
        {
            item.RemoveAction();
            _toRemove.Add(item);
        }

        public void CommitRemoves()
        {
            foreach (var item in _toRemove) _items.Remove(item);
            _toRemove.Clear();
        }

        public void Clear()
        {
            foreach (var item in _items) item.RemoveAction();
            _items.Clear();
            _toRemove.Clear();
        }

        public IEnumerator<GameAction> GetEnumerator()
        {
            return _items.GetEnumerator();
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return _items.GetEnumerator();
        }

        public void Serialize(NetworkBinaryWriter writer, SerializationModeFlags mode)
        {
            checked
            {
                if ((mode & SerializationModeFlags.VaryingData) != 0)
                {
                    writer.Write((byte)_items.Count);
                    foreach (var item in _items)
                    {
                        writer.Write((byte)item.TypeID);
                        item.Serialize(writer, SerializationModeFlags.All);
                    }
                }
            }
        }

        public void Deserialize(NetworkBinaryReader reader, SerializationModeFlags mode, int framesAgo)
        {
            if ((mode & SerializationModeFlags.VaryingData) != 0)
            {
                int count = reader.ReadByte();
                var currentItems = new List<GameAction>();
                for (int i = 0; i < count; ++i)
                {
                    int typeID = reader.ReadByte();
                    var item = GameAction.CreateGameAction(typeID);
                    item.Deserialize(reader, SerializationModeFlags.All, framesAgo);
                    item.Player = _owner;
                    currentItems.Add(item);
                }
                UpdateItems(currentItems);
            }
        }

        private void UpdateItems(List<GameAction> currentItems)
        {
            // Remove missing items
            for (int i = _items.Count - 1; i >= 0 ; i--)
			{
                if (!currentItems.Any(x => x.TypeID == _items[i].TypeID))
                    _items.RemoveAt(i);
            }

            // Add new items
            foreach (var item in currentItems)
                if (!_items.Any(x => x.TypeID == item.TypeID))
                {
                    if (item.DoAction()) _items.Add(item);
                }
        }
    }
}