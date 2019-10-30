﻿using MMRando.Asm;
using MMRando.Attributes;
using MMRando.Constants;
using MMRando.Extensions;
using MMRando.GameObjects;
using MMRando.Models;
using MMRando.Models.Rom;
using MMRando.Models.Settings;
using MMRando.Models.SoundEffects;
using MMRando.Utils;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;

namespace MMRando
{

    public class Builder
    {
        private RandomizedResult _randomized;
        private SettingsObject _settings;
        private MessageTable _messageTable;

        public Builder(RandomizedResult randomized)
        {
            _randomized = randomized;
            _settings = randomized.Settings;
            _messageTable = new MessageTable();
        }

        #region Sequences, sounds and BGM

        private void BGMShuffle(Random random)
        {
            while (RomData.TargetSequences.Count > 0)
            {
                List<SequenceInfo> Unassigned = RomData.SequenceList.FindAll(u => u.Replaces == -1);

                int targetIndex = random.Next(RomData.TargetSequences.Count);
                var targetSequence = RomData.TargetSequences[targetIndex];

                while (true)
                {
                    int unassignedIndex = random.Next(Unassigned.Count);

                    if (Unassigned[unassignedIndex].Name.StartsWith("mm")
                        & (random.Next(100) < 50))
                    {
                        continue;
                    }

                    for (int i = 0; i < Unassigned[unassignedIndex].Type.Count; i++)
                    {
                        if (targetSequence.Type.Contains(Unassigned[unassignedIndex].Type[i]))
                        {
                            Unassigned[unassignedIndex].Replaces = targetSequence.Replaces;
                            Debug.WriteLine(Unassigned[unassignedIndex].Name + " -> " + targetSequence.Name);
                            RomData.TargetSequences.RemoveAt(targetIndex);
                            break;
                        }
                        else if (i + 1 == Unassigned[unassignedIndex].Type.Count)
                        {
                            if ((random.Next(30) == 0)
                                && ((Unassigned[unassignedIndex].Type[0] & 8) == (targetSequence.Type[0] & 8))
                                && (Unassigned[unassignedIndex].Type.Contains(10) == targetSequence.Type.Contains(10))
                                && (!Unassigned[unassignedIndex].Type.Contains(16)))
                            {
                                Unassigned[unassignedIndex].Replaces = targetSequence.Replaces;
                                Debug.WriteLine(Unassigned[unassignedIndex].Name + " -> " + targetSequence.Name);
                                RomData.TargetSequences.RemoveAt(targetIndex);
                                break;
                            }
                        }
                    }

                    if (Unassigned[unassignedIndex].Replaces != -1)
                    {
                        break;
                    }
                }
            }

            RomData.SequenceList.RemoveAll(u => u.Replaces == -1);
        }

        #endregion

        private void WriteAudioSeq(Random random)
        {
            if (_settings.Music != Music.Random)
            {
                return;
            }

            SequenceUtils.ReadSequenceInfo();
            BGMShuffle(random);

            foreach (SequenceInfo s in RomData.SequenceList)
            {
                s.Name = Values.MusicDirectory + s.Name;
            }

            ResourceUtils.ApplyHack(Values.ModsDirectory + "fix-music");
            ResourceUtils.ApplyHack(Values.ModsDirectory + "inst24-swap-guitar");
            SequenceUtils.RebuildAudioSeq(RomData.SequenceList);
        }

        private void WriteMuteMusic()
        {
            if (_settings.Music == Music.None)
            {
                var codeFileAddress = 0xB3C000;
                var offset = 0x102350; // address for branch when scene music is loaded
                ReadWriteUtils.WriteToROM(codeFileAddress + offset, 0x1000); // change to always branch (do not load)
            }
        }

        private void WritePlayerModel()
        {
            if (_settings.Character == Character.LinkMM)
            {
                return;
            }

            int characterIndex = (int)_settings.Character;

            using (var b = new BinaryReader(File.Open($"{Values.ObjsDirectory}link-{characterIndex}", FileMode.Open)))
            {
                var obj = new byte[b.BaseStream.Length];
                b.Read(obj, 0, obj.Length);

                ResourceUtils.ApplyHack($"{Values.ModsDirectory}fix-link-{characterIndex}");
                ObjUtils.InsertObj(obj, 0x11);
            }

            if (_settings.Character == Character.Kafei)
            {
                using (var b = new BinaryReader(File.Open($"{Values.ObjsDirectory}kafei", FileMode.Open)))
                {
                    var obj = new byte[b.BaseStream.Length];
                    b.Read(obj, 0, obj.Length);

                    ObjUtils.InsertObj(obj, 0x1C);
                    ResourceUtils.ApplyHack(Values.ModsDirectory + "fix-kafei");
                }
            }
        }

        private void WriteTunicColor()
        {
            Color t = _settings.TunicColor;
            byte[] color = { t.R, t.G, t.B };

            var otherTunics = ResourceUtils.GetAddresses(Values.AddrsDirectory + "tunic-forms");
            TunicUtils.UpdateFormTunics(otherTunics, _settings.TunicColor);

            var playerModel = DeterminePlayerModel();
            var characterIndex = (int)playerModel;
            var locations = ResourceUtils.GetAddresses($"{Values.AddrsDirectory}tunic-{characterIndex}");
            var objectIndex = playerModel == Character.Kafei ? 0x1C : 0x11;
            var objectData = ObjUtils.GetObjectData(objectIndex);
            for (int j = 0; j < locations.Count; j++)
            {
                ReadWriteUtils.WriteFileAddr(locations[j], color, objectData);
            }
            ObjUtils.InsertObj(objectData, objectIndex);
        }

        private Character DeterminePlayerModel()
        {
            var data = ObjUtils.GetObjectData(0x11);
            if (data[0x107] == 0x05)
            {
                return Character.LinkMM;
            }
            if (data[0x107] == 0x07)
            {
                return Character.LinkOOT;
            }
            if (data[0xC6] == 0x02)
            {
                return Character.AdultLink;
            }
            if (data[0xC5] == 0x15)
            {
                return Character.Kafei;
            }
            throw new InvalidOperationException("Unable to determine player's model.");
        }

        private void WriteTatlColour()
        {
            if (_settings.TatlColorSchema != TatlColorSchema.Random)
            {
                var selectedColorSchemaIndex = (int)_settings.TatlColorSchema;
                byte[] c = new byte[8];
                List<int[]> locs = ResourceUtils.GetAddresses(Values.AddrsDirectory + "tatl-colour");
                for (int i = 0; i < locs.Count; i++)
                {
                    ReadWriteUtils.Arr_WriteU32(c, 0, Values.TatlColours[selectedColorSchemaIndex, i << 1]);
                    ReadWriteUtils.Arr_WriteU32(c, 4, Values.TatlColours[selectedColorSchemaIndex, (i << 1) + 1]);
                    ReadWriteUtils.WriteROMAddr(locs[i], c);
                }
            }
            else
            {
                ResourceUtils.ApplyHack(Values.ModsDirectory + "rainbow-tatl");
            }
        }

        private void WriteQuickText()
        {
            if (_settings.QuickTextEnabled)
            {
                ResourceUtils.ApplyHack(Values.ModsDirectory + "quick-text");
            }
        }

        private void WriteCutscenes()
        {
            if (_settings.ShortenCutscenes)
            {
                ResourceUtils.ApplyHack(Values.ModsDirectory + "short-cutscenes");
            }
        }

        private void WriteDungeons()
        {
            if ((_settings.LogicMode == LogicMode.Vanilla) || (!_settings.RandomizeDungeonEntrances))
            {
                return;
            }

            EntranceUtils.WriteEntrances(Values.OldEntrances.ToArray(), _randomized.NewEntrances);
            EntranceUtils.WriteEntrances(Values.OldExits.ToArray(), _randomized.NewExits);
            byte[] li = new byte[] { 0x24, 0x02, 0x00, 0x00 };
            List<int[]> addr = new List<int[]>();
            addr = ResourceUtils.GetAddresses(Values.AddrsDirectory + "d-check");
            for (int i = 0; i < addr.Count; i++)
            {
                li[3] = (byte)_randomized.NewExitIndices[i];
                ReadWriteUtils.WriteROMAddr(addr[i], li);
            }

            ResourceUtils.ApplyHack(Values.ModsDirectory + "fix-dungeons");
            addr = ResourceUtils.GetAddresses(Values.AddrsDirectory + "d-exit");

            for (int i = 0; i < addr.Count; i++)
            {
                if (i == 2)
                {
                    ReadWriteUtils.WriteROMAddr(addr[i], new byte[] {
                        (byte)((Values.OldExits[_randomized.NewDestinationIndices[i + 1]] & 0xFF00) >> 8),
                        (byte)(Values.OldExits[_randomized.NewDestinationIndices[i + 1]] & 0xFF) });
                }
                else
                {
                    ReadWriteUtils.WriteROMAddr(addr[i], new byte[] {
                        (byte)((Values.OldExits[_randomized.NewDestinationIndices[i]] & 0xFF00) >> 8),
                        (byte)(Values.OldExits[_randomized.NewDestinationIndices[i]] & 0xFF) });
                }
            }

            addr = ResourceUtils.GetAddresses(Values.AddrsDirectory + "dc-flagload");
            for (int i = 0; i < addr.Count; i++)
            {
                ReadWriteUtils.WriteROMAddr(addr[i], new byte[] { (byte)((_randomized.NewDCFlags[i] & 0xFF00) >> 8), (byte)(_randomized.NewDCFlags[i] & 0xFF) });
            }

            addr = ResourceUtils.GetAddresses(Values.AddrsDirectory + "dc-flagmask");
            for (int i = 0; i < addr.Count; i++)
            {
                ReadWriteUtils.WriteROMAddr(addr[i], new byte[] {
                    (byte)((_randomized.NewDCMasks[i] & 0xFF00) >> 8),
                    (byte)(_randomized.NewDCMasks[i] & 0xFF) });
            }
        }

        private void WriteSpeedUps()
        {
            if (_settings.SpeedupBeavers)
            {
                ResourceUtils.ApplyHack(Values.ModsDirectory + "speedup-beavers");
                _messageTable.UpdateMessages(new MessageEntry
                {
                    Id = 0x10D6,
                    Header = null,
                    Message = "\u001E\u0029\u001AThere's a total of \u000125 rings\u0000. You must swim through them in the right order for it to count. Swim through the ring that's \u0001flashing\u0000.".Wrap(35, "\u0011") + "\u0010My big brother will show you the way, so follow him and don't get separated!\u00BF".Wrap(35, "\u0011")
                });
                _messageTable.UpdateMessages(new MessageEntry
                {
                    Id = 0x10FA,
                    Header = null,
                    Message = "\u001E\u0029\u0019This time, the limit is \u00011:50\u0000.".EndTextbox() + "Don't fall behind!\u00BF"
                });
                _messageTable.UpdateMessages(new MessageEntry
                {
                    Id = 0x1107,
                    Header = null,
                    Message = "\u001E\u0029\u0019This time around, you have to beat \u00011:40\u0000.".Wrap(35, "\u0011").EndTextbox() + "Don't fall behind!\u00BF"
                });
            }

            if (_settings.SpeedupDampe)
            {
                ResourceUtils.ApplyHack(Values.ModsDirectory + "speedup-dampe");
            }

            if (_settings.SpeedupLabFish)
            {
                ResourceUtils.ApplyHack(Values.ModsDirectory + "speedup-labfish");
            }

            if (_settings.SpeedupDogRace)
            {
                ResourceUtils.ApplyHack(Values.ModsDirectory + "speedup-dograce");
            }
        }

        private void WriteGimmicks()
        {
            int damageMultiplier = (int)_settings.DamageMode;
            if (damageMultiplier > 0)
            {
                ResourceUtils.ApplyHack(Values.ModsDirectory + "dm-" + damageMultiplier.ToString());
            }

            int damageEffect = (int)_settings.DamageEffect;
            if (damageEffect > 0)
            {
                ResourceUtils.ApplyHack(Values.ModsDirectory + "de-" + damageEffect.ToString());
            }

            int gravityType = (int)_settings.MovementMode;
            if (gravityType > 0)
            {
                ResourceUtils.ApplyHack(Values.ModsDirectory + "movement-" + gravityType.ToString());
            }

            int floorType = (int)_settings.FloorType;
            if (floorType > 0)
            {
                ResourceUtils.ApplyHack(Values.ModsDirectory + "floor-" + floorType.ToString());
            }

            if (_settings.ClockSpeed != ClockSpeed.Default)
            {
                WriteClockSpeed(_settings.ClockSpeed);
            }

            if (_settings.HideClock)
            {
                WriteHideClock();
            }

            if (_settings.BlastMaskCooldown != BlastMaskCooldown.Default)
            {
                WriteBlastMaskCooldown();
            }
        }

        private void WriteBlastMaskCooldown()
        {
            ushort value;
            switch (_settings.BlastMaskCooldown)
            {
                default:
                case BlastMaskCooldown.Default:
                    value = 0x136; // 310 frames 
                    break;
                case BlastMaskCooldown.Instant:
                    value = 0x1; // 1 frame
                    break;
                case BlastMaskCooldown.VeryShort:
                    value = 0x20; // 32 frames
                    break;
                case BlastMaskCooldown.Short:
                    value = 0x80; // 128 frames
                    break;
                case BlastMaskCooldown.Long:
                    value = 0x200; // 512 frames
                    break;
                case BlastMaskCooldown.VeryLong:
                    value = 0x400; // 1024 frames
                    break;
            }

            var codeFileAddress = 0x00CA7F00;
            var offset = 0x002766;
            ReadWriteUtils.WriteToROM(codeFileAddress + offset, value);
        }

        private void WriteHideClock()
        {
            var codeFileAddress = 0xB3C000;
            var offset = 0x73B7C; // branch for UI is time hasn't changed
            ReadWriteUtils.WriteToROM(codeFileAddress + offset, 0x10); // change to always branch
        }

        /// <summary>
        /// Overwrite the clockspeed (see Settings.ClockSpeed for details)
        /// </summary>
        /// <param name="clockSpeed"></param>
        private void WriteClockSpeed(ClockSpeed clockSpeed)
        {
            byte speed;
            short invertedModifier;
            switch (clockSpeed)
            {
                default:
                case ClockSpeed.Default:
                    speed = 3;
                    invertedModifier = -2;
                    break;
                case ClockSpeed.VerySlow:
                    speed = 1;
                    invertedModifier = 0;
                    break;
                case ClockSpeed.Slow:
                    speed = 2;
                    invertedModifier = -1;
                    break;
                case ClockSpeed.Fast:
                    speed = 6;
                    invertedModifier = -4;
                    break;
                case ClockSpeed.VeryFast:
                    speed = 9;
                    invertedModifier = -6;
                    break;
                case ClockSpeed.SuperFast:
                    speed = 18;
                    invertedModifier = -12;
                    break;
            }

            ResourceUtils.ApplyHack(Values.ModsDirectory + "fix-clock-speed");

            var codeFileAddress = 0xB3C000;
            var hackAddressOffset = 0x8A674;
            var modificationOffset = 0x1B;
            ReadWriteUtils.WriteToROM(codeFileAddress + hackAddressOffset + modificationOffset, speed);

            var invertedModifierOffsets = new List<int>
            {
                0xB1B8E,
                0x7405E
            };
            foreach (var offset in invertedModifierOffsets)
            {
                ReadWriteUtils.WriteToROM(codeFileAddress + offset, (ushort)invertedModifier);
            }
        }

        /// <summary>
        /// Update the gossip stone actor to not check mask of truth
        /// </summary>
        private void WriteFreeHints()
        {
            int address = 0x00E0A810 + 0x378;
            byte val = 0x00;
            ReadWriteUtils.WriteToROM(address, val);
        }

        private void WriteSoundEffects(Random random)
        {
            if (!_randomized.Settings.RandomizeSounds)
            {
                return;
            }

            var shuffledSoundEffects = new Dictionary<SoundEffect, SoundEffect>();

            var replacableSounds = SoundEffects.Replacable();
            foreach (var sound in replacableSounds)
            {
                var soundPool = SoundEffects.FilterByTags(sound.ReplacableByTags());

                if (soundPool.Count > 0)
                {
                    shuffledSoundEffects[sound] = soundPool.Random(random);
                }
            }

            foreach (var sounds in shuffledSoundEffects)
            {
                var oldSound = sounds.Key;
                var newSound = sounds.Value;

                if (oldSound.IsReplacableInMessage())
                {
                    oldSound.ReplaceInMessageWith(newSound, _messageTable);
                }
                else
                {
                    oldSound.ReplaceWith(newSound);
                }
                Debug.WriteLine($"Writing SFX {newSound} --> {oldSound}");
            }
        }

        private void SoundEffectShuffle()
        {
        }

        private void WriteEnemies()
        {
            if (_settings.RandomizeEnemies)
            {
                Enemies.ShuffleEnemies(_randomized.Random);
            }
        }

        private void PutOrCombine(Dictionary<int, byte> dictionary, int key, byte value, bool add = false)
        {
            if (!dictionary.ContainsKey(key))
            {
                dictionary[key] = 0;
            }
            dictionary[key] = add ? (byte)(dictionary[key] + value) : (byte)(dictionary[key] | value);
        }

        private void WriteFreeItems(params Item[] items)
        {
            Dictionary<int, byte> startingItems = new Dictionary<int, byte>();
            PutOrCombine(startingItems, 0xC5CE72, 0x10); // add Song of Time

            var itemList = items.ToList();

            if (_settings.CustomStartingItemList != null)
            {
                itemList.AddRange(_settings.CustomStartingItemList);
            }

            itemList.Add(Item.StartingHeartContainer1);
            while (itemList.Count(item => item.Name() == "Piece of Heart") >= 4)
            {
                itemList.Add(Item.StartingHeartContainer1);
                for (var i = 0; i < 4; i++)
                {
                    var heartPiece = itemList.First(item => item.Name() == "Piece of Heart");
                    itemList.Remove(heartPiece);
                }
            }

            itemList = itemList
                .GroupBy(item => ItemUtils.ForbiddenStartTogether.FirstOrDefault(fst => fst.Contains(item)))
                .SelectMany(g => g.Key == null ? g.ToList() : g.OrderByDescending(item => g.Key.IndexOf(item)).Take(1))
                .ToList();

            foreach (var item in itemList)
            {
                var startingItemValues = item.GetAttributes<StartingItemAttribute>();
                if (!startingItemValues.Any() && !_settings.NoStartingItems)
                {
                    throw new Exception($@"Invalid starting item ""{item}""");
                }
                foreach (var startingItem in startingItemValues)
                {
                    PutOrCombine(startingItems, startingItem.Address, startingItem.Value, startingItem.IsAdditional);
                }
            }

            foreach (var kvp in startingItems)
            {
                ReadWriteUtils.WriteToROM(kvp.Key, kvp.Value);
            }

            if (itemList.Count(item => item.Name() == "Heart Container") == 1)
            {
                ReadWriteUtils.WriteToROM(0x00B97E8F, 0x0C); // reduce low health beep threshold
            }
        }

        private void WriteItems()
        {
            var freeItems = new List<Item>();
            if (_settings.LogicMode == LogicMode.Vanilla)
            {
                freeItems.Add(Item.FairyMagic);
                freeItems.Add(Item.MaskDeku);
                freeItems.Add(Item.SongHealing);
                freeItems.Add(Item.StartingSword);
                freeItems.Add(Item.StartingShield);
                freeItems.Add(Item.StartingHeartContainer1);
                freeItems.Add(Item.StartingHeartContainer2);

                if (_settings.ShortenCutscenes)
                {
                    //giants cs were removed
                    freeItems.Add(Item.SongOath);
                }

                WriteFreeItems(freeItems.ToArray());

                return;
            }

            //write free item (start item default = Deku Mask)
            freeItems.Add(_randomized.ItemList.Find(u => u.NewLocation == Item.MaskDeku).Item);
            freeItems.Add(_randomized.ItemList.Find(u => u.NewLocation == Item.SongHealing).Item);
            freeItems.Add(_randomized.ItemList.Find(u => u.NewLocation == Item.StartingSword).Item);
            freeItems.Add(_randomized.ItemList.Find(u => u.NewLocation == Item.StartingShield).Item);
            freeItems.Add(_randomized.ItemList.Find(u => u.NewLocation == Item.StartingHeartContainer1).Item);
            freeItems.Add(_randomized.ItemList.Find(u => u.NewLocation == Item.StartingHeartContainer2).Item);
            WriteFreeItems(freeItems.ToArray());

            //write everything else
            ItemSwapUtils.ReplaceGetItemTable(Values.ModsDirectory);
            ItemSwapUtils.InitItems();

            if (_settings.FixEponaSword)
            {
                ResourceUtils.ApplyHack(Values.ModsDirectory + "fix-epona");
            }
            if (_settings.PreventDowngrades)
            {
                ResourceUtils.ApplyHack(Values.ModsDirectory + "fix-downgrades");
            }
            if (_settings.AddCowMilk)
            {
                ResourceUtils.ApplyHack(Values.ModsDirectory + "fix-cow-bottle-check");
            }

            var newMessages = new List<MessageEntry>();
            foreach (var item in _randomized.ItemList)
            {
                // Unused item
                if (item.NewLocation == null)
                {
                    continue;
                }

                if (ItemUtils.IsBottleCatchContent(item.Item))
                {
                    ItemSwapUtils.WriteNewBottle(item.NewLocation.Value, item.Item);
                }
                else
                {
                    ChestTypeAttribute.ChestType? overrideChestType = null;
                    if ((item.Item.Name().Contains("Bombchu") || item.Item.Name().Contains("Shield")) && _randomized.Logic.Any(il => il.RequiredItemIds?.Contains(item.ID) == true || il.ConditionalItemIds?.Any(c => c.Contains(item.ID)) == true))
                    {
                        overrideChestType = ChestTypeAttribute.ChestType.LargeGold;
                    }
                    ItemSwapUtils.WriteNewItem(item.NewLocation.Value, item.Item, newMessages, _settings.UpdateShopAppearance, _settings.PreventDowngrades, _settings.UpdateChests && item.IsRandomized, overrideChestType, _settings.CustomStartingItemList.Contains(item.Item));
                }
            }

            var copyRupeesRegex = new Regex(": [0-9]+ Rupees");
            foreach (var newMessage in newMessages)
            {
                var oldMessage = _messageTable.GetMessage(newMessage.Id);
                if (oldMessage != null)
                {
                    var cost = copyRupeesRegex.Match(oldMessage.Message).Value;
                    newMessage.Message = copyRupeesRegex.Replace(newMessage.Message, cost);
                }
            }

            if (_settings.UpdateShopAppearance)
            {
                // update tingle shops
                foreach (var messageShopText in Enum.GetValues(typeof(MessageShopText)).Cast<MessageShopText>())
                {
                    var messageShop = messageShopText.GetAttribute<MessageShopAttribute>();
                    var item1 = _randomized.ItemList.First(io => io.NewLocation == messageShop.Items[0]).Item;
                    var item2 = _randomized.ItemList.First(io => io.NewLocation == messageShop.Items[1]).Item;
                    newMessages.Add(new MessageEntry
                    {
                        Id = (ushort)messageShopText,
                        Header = null,
                        Message = string.Format(messageShop.MessageFormat, item1.Name() + " ", messageShop.Prices[0], item2.Name() + " ", messageShop.Prices[1])
                    });
                }

                // update business scrub
                var businessScrubItem = _randomized.ItemList.First(io => io.NewLocation == Item.HeartPieceTerminaBusinessScrub).Item;
                newMessages.Add(new MessageEntry
                {
                    Id = 0x1631,
                    Header = null,
                    Message = $"\x1E\x3A\xD2Please! I'll sell you {MessageUtils.GetArticle(businessScrubItem)}\u0001{businessScrubItem.Name()}\u0000 if you just keep this place a secret...\x19\xBF".Wrap(35, "\u0011")
                });
                newMessages.Add(new MessageEntry
                {
                    Id = 0x1632,
                    Header = null,
                    Message = $"\u0006150 Rupees\u0000 for{MessageUtils.GetPronounOrAmount(businessScrubItem).ToLower()}!\u0011 \u0011\u0002\u00C2I'll buy {MessageUtils.GetPronoun(businessScrubItem)}\u0011No thanks\u00BF"
                });
                newMessages.Add(new MessageEntry
                {
                    Id = 0x1634,
                    Header = null,
                    Message = $"What about{MessageUtils.GetPronounOrAmount(businessScrubItem, "").ToLower()} for \u0006100 Rupees\u0000?\u0011 \u0011\u0002\u00C2I'll buy {MessageUtils.GetPronoun(businessScrubItem)}\u0011No thanks\u00BF"
                });

                // update biggest bomb bag purchase
                var biggestBombBagItem = _randomized.ItemList.First(io => io.NewLocation == Item.UpgradeBiggestBombBag).Item;
                newMessages.Add(new MessageEntry
                {
                    Id = 0x15F5,
                    Header = null,
                    Message = $"I sell {MessageUtils.GetArticle(biggestBombBagItem)}\u0001{MessageUtils.GetAlternateName(biggestBombBagItem)}\u0000, but I'm focusing my marketing efforts on \u0001Gorons\u0000.".Wrap(35, "\u0011").EndTextbox() + "What I'd really like to do is go back home and do business where I'm surrounded by trees and grass.\u0019\u00BF".Wrap(35, "\u0011")
                });
                newMessages.Add(new MessageEntry
                {
                    Id = 0x15FF,
                    Header = null,
                    Message = $"\x1E\x39\x8CRight now, I've got a \u0001special\u0011\u0000offer just for you.\u0019\u00BF"
                });
                newMessages.Add(new MessageEntry
                {
                    Id = 0x1600,
                    Header = null,
                    Message = $"\x1E\x38\x81I'll give you {MessageUtils.GetArticle(biggestBombBagItem, "my ")}\u0001{biggestBombBagItem.Name()}\u0000, regularly priced at \u00061000 Rupees\u0000...".Wrap(35, "\u0011").EndTextbox() + "In return, you'll give me just\u0011\u0006200 Rupees\u0000!\u0019\u00BF"
                });
                newMessages.Add(new MessageEntry
                {
                    Id = 0x1606,
                    Header = null,
                    Message = $"\x1E\x38\x81I'll give you {MessageUtils.GetArticle(biggestBombBagItem, "my ")}\u0001{biggestBombBagItem.Name()}\u0000, regularly priced at \u00061000 Rupees\u0000, for just \u0006200 Rupees\u0000!\u0019\u00BF".Wrap(35, "\u0011")
                });

                // update swamp scrub purchase
                var magicBeanItem = _randomized.ItemList.First(io => io.NewLocation == Item.ShopItemBusinessScrubMagicBean).Item;
                newMessages.Add(new MessageEntry
                {
                    Id = 0x15E1,
                    Header = null,
                    Message = $"\x1E\x39\xA7I'm selling {MessageUtils.GetArticle(magicBeanItem)}\u0001{MessageUtils.GetAlternateName(magicBeanItem)}\u0000 to Deku Scrubs, but I'd really like to leave my hometown.".Wrap(35, "\u0011").EndTextbox() + "I'm hoping to find some success in a livelier place!\u0019\u00BF".Wrap(35, "\u0011")
                });

                newMessages.Add(new MessageEntry
                {
                    Id = 0x15E9,
                    Header = null,
                    Message = $"\x1E\x3A\u00D2Do you know what {MessageUtils.GetArticle(magicBeanItem)}\u0001{MessageUtils.GetAlternateName(magicBeanItem)}\u0000 {MessageUtils.GetVerb(magicBeanItem)}, sir?".Wrap(35, "\u0011") + $"\u0011I'll sell you{MessageUtils.GetPronounOrAmount(magicBeanItem).ToLower()} for \u000610 Rupees\u0000.\u0019\u00BF"
                });

                // update ocean scrub purchase
                var greenPotionItem = _randomized.ItemList.First(io => io.NewLocation == Item.ShopItemBusinessScrubGreenPotion).Item;
                newMessages.Add(new MessageEntry
                {
                    Id = 0x1608,
                    Header = null,
                    Message = $"\x1E\x39\xA7I'm selling {MessageUtils.GetArticle(greenPotionItem)}\u0001{MessageUtils.GetAlternateName(greenPotionItem)}\u0000, but I'm focusing my marketing efforts on Zoras.".Wrap(35, "\u0011").EndTextbox() + "Actually, I'd like to do business someplace where it's cooler and the air is clean.\u0019\u00BF".Wrap(35, "\u0011")
                });

                newMessages.Add(new MessageEntry
                {
                    Id = 0x1612,
                    Header = null,
                    Message = $"\x1E\x39\x8CI'll sell you {MessageUtils.GetArticle(greenPotionItem)}\u0001{greenPotionItem.Name()}\u0000 for \u000640 Rupees\u0000!\u00E0\u00BF".Wrap(35, "\u0011")
                });

                // update canyon scrub purchase
                var bluePotionItem = _randomized.ItemList.First(io => io.NewLocation == Item.ShopItemBusinessScrubBluePotion).Item;
                newMessages.Add(new MessageEntry
                {
                    Id = 0x161C,
                    Header = null,
                    Message = $"\x1E\x39\xA7I'm here to sell {MessageUtils.GetArticle(bluePotionItem)}\u0001{MessageUtils.GetAlternateName(bluePotionItem)}\u0000.".Wrap(35, "\u0011").EndTextbox() + "Actually, I want to do business in the sea breeze while listening to the sound of the waves.\u0019\u00BF".Wrap(35, "\u0011")
                });

                newMessages.Add(new MessageEntry
                {
                    Id = 0x1626,
                    Header = null,
                    Message = $"\x1E\x3A\u00D2Don't you need {MessageUtils.GetArticle(bluePotionItem)}\u0001{MessageUtils.GetAlternateName(bluePotionItem)}\u0000? I'll sell you{MessageUtils.GetPronounOrAmount(bluePotionItem).ToLower()} for \u0006100 Rupees\u0000.\u0019\u00BF".Wrap(35, "\u0011")
                });

                newMessages.Add(new MessageEntry
                {
                    Id = 0x15EA,
                    Header = null,
                    Message = $"Do we have a deal?\u0011 \u0011\u0002\u00C2Yes\u0011No\u00BF"
                });

                // update gorman bros milk purchase
                var gormanBrosMilkItem = _randomized.ItemList.First(io => io.NewLocation == Item.ShopItemGormanBrosMilk).Item;
                newMessages.Add(new MessageEntry
                {
                    Id = 0x3463,
                    Header = null,
                    Message = $"Won'tcha buy {MessageUtils.GetArticle(gormanBrosMilkItem)}\u0001{MessageUtils.GetAlternateName(gormanBrosMilkItem)}\u0000?\u0019\u00BF".Wrap(35, "\u0011")
                });
                newMessages.Add(new MessageEntry
                {
                    Id = 0x3466,
                    Header = null,
                    Message = $"\u000650 Rupees\u0000 will do ya for{MessageUtils.GetPronounOrAmount(gormanBrosMilkItem).ToLower()}.\u0011 \u0011\u0002\u00C2I'll buy {MessageUtils.GetPronoun(gormanBrosMilkItem)}\u0011No thanks\u00BF"
                });
                newMessages.Add(new MessageEntry
                {
                    Id = 0x346B,
                    Header = null,
                    Message = $"Buyin' {MessageUtils.GetArticle(gormanBrosMilkItem)}\u0001{MessageUtils.GetAlternateName(gormanBrosMilkItem)}\u0000?\u0019\u00BF".Wrap(35, "\u0011")
                });
                newMessages.Add(new MessageEntry
                {
                    Id = 0x348F,
                    Header = null,
                    Message = $"Seems like we're the only ones who have {MessageUtils.GetArticle(gormanBrosMilkItem)}\u0001{MessageUtils.GetAlternateName(gormanBrosMilkItem)}\u0000. Hyuh, hyuh. If you like, I'll sell you{MessageUtils.GetPronounOrAmount(gormanBrosMilkItem).ToLower()}.\u0019\u00BF".Wrap(35, "\u0011")
                });
                newMessages.Add(new MessageEntry
                {
                    Id = 0x3490,
                    Header = null,
                    Message = $"\u000650 Rupees\u0000 will do you for{MessageUtils.GetPronounOrAmount(gormanBrosMilkItem).ToLower()}!\u0011 \u0011\u0002\u00C2I'll buy {MessageUtils.GetPronoun(gormanBrosMilkItem)}\u0011No thanks\u00BF"
                });

                // update lottery message
                var lotteryItem = _randomized.ItemList.First(io => io.NewLocation == Item.MundaneItemLotteryPurpleRupee).Item;
                newMessages.Add(new MessageEntry
                {
                    Id = 0x2B5C,
                    Header = null,
                    Message = $"Would you like the chance to buy your dreams for \u000610 Rupees\u0000?".Wrap(35, "\u0011").EndTextbox() + $"Pick any three numbers, and if those are picked, you'll win {MessageUtils.GetArticle(lotteryItem)}\u0001{lotteryItem.Name()}\u0000. It's only for the \u0001first\u0000 person!\u0019\u00BF".Wrap(35, "\u0011")
                });

            }

            // replace "Razor Sword is now blunt" message with get-item message for Kokiri Sword.
            newMessages.Add(new MessageEntry
            {
                Id = 0xF9,
                Header = new byte[11] { 0x06, 0x00, 0xFE, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF },
                Message = $"You got the \x01Kokiri Sword\x00!\u0011This is a hidden treasure of\u0011the Kokiri, but you can borrow it\u0011for a while.\u00BF",
            });

            // replace Magic Power message
            newMessages.Add(new MessageEntry
            {
                Id = 0xC8,
                Header = null,
                Message = $"\u0017You've been granted \u0002Magic Power\u0000!\u0018\u0011Replenish it with \u0001Magic Jars\u0000\u0011and \u0001Potions\u0000.\u00BF",
            });

            // update Bank Reward messages
            newMessages.Add(new MessageEntry
            {
                Id = 0x45C,
                Header = null,
                Message = "\u0017What's this? You've already saved\u0011up \u0001500 Rupees\u0000!?!\u0018\u0011\u0013\u0012Well, little guy, here's your special\u0011gift. Take it!\u00E0\u00BF",
            });
            newMessages.Add(new MessageEntry
            {
                Id = 0x45D,
                Header = null,
                Message = "\u0017What's this? You've already saved\u0011up \u00011000 Rupees\u0000?!\u0018\u0011\u0013\u0012Well, little guy, I can't take any\u0011more deposits. Sorry, but this is\u0011all I can give you.\u00E0\u00BF",
            });

            if (_settings.AddSkulltulaTokens)
            {
                ResourceUtils.ApplyHack(Values.ModsDirectory + "fix-skulltula-tokens");

                newMessages.Add(new MessageEntry
                {
                    Id = 0x51,
                    Header = new byte[11] { 0x02, 0x00, 0x52, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF },
                    Message = $"\u0017You got an \u0005Ocean Gold Skulltula\u0011Spirit\0!\u0018\u001F\u0000\u0010 This is your \u0001\u000D\u0000 one!\u00BF",
                });
                newMessages.Add(new MessageEntry
                {
                    Id = 0x52,
                    Header = new byte[11] { 0x02, 0x00, 0x52, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF },
                    Message = $"\u0017You got a \u0006Swamp Gold Skulltula\u0011Spirit\0!\u0018\u001F\u0000\u0010 This is your \u0001\u000D\u0000 one!\u00BF",
                });
            }

            if (_settings.AddStrayFairies)
            {
                ResourceUtils.ApplyHack(Values.ModsDirectory + "fix-fairies");
            }

            var dungeonItemMessageIds = new byte[] {
                0x3C, 0x3D, 0x3E, 0x3F, 0x74,
                0x40, 0x4D, 0x4E, 0x53, 0x75,
                0x54, 0x61, 0x64, 0x6E, 0x76,
                0x70, 0x71, 0x72, 0x73, 0x77,
            };

            var dungeonNames = new string[]
            {
                "\u0006Woodfall Temple\u0000",
                "\u0002Snowhead Temple\u0000",
                "\u0005Great Bay Temple\u0000",
                "\u0004Stone Tower Temple\u0000"
            };

            var dungeonItemMessages = new string[]
            {
                "\u0017You found a \u0001Small Key\u0000 for\u0011{0}!\u0018\u00BF",
                "\u0017You found the \u0001Boss Key\u0000 for\u0011{0}!\u0018\u00BF",
                "\u0017You found the \u0001Dungeon Map\u0000 for\u0011{0}!\u0018\u00BF",
                "\u0017You found the \u0001Compass\u0000 for\u0011{0}!\u0018\u00BF",
                "\u0017You found a \u0001Stray Fairy\u0000 from\u0011{0}!\u0018\u001F\u0000\u0010\u0011This is your \u0001\u000C\u0000 one!\u00BF",
            };

            var dungeonItemIcons = new byte[]
            {
                0x3C, 0x3D, 0x3E, 0x3F, 0xFE
            };

            for (var i = 0; i < dungeonItemMessageIds.Length; i++)
            {
                var messageId = dungeonItemMessageIds[i];
                var icon = dungeonItemIcons[i % 5];
                var dungeonName = dungeonNames[i / 5];
                var message = string.Format(dungeonItemMessages[i % 5], dungeonName);

                newMessages.Add(new MessageEntry
                {
                    Id = messageId,
                    Header = new byte[11] { 0x02, 0x00, icon, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF },
                    Message = message
                });
            }

            _messageTable.UpdateMessages(newMessages);

            if (_settings.AddShopItems)
            {
                ResourceUtils.ApplyHack(Values.ModsDirectory + "fix-shop-checks");
            }
        }

        private void WriteGossipQuotes()
        {
            if (_settings.LogicMode == LogicMode.Vanilla)
            {
                return;
            }

            if (_settings.FreeHints)
            {
                WriteFreeHints();
            }

            if (_settings.GossipHintStyle != GossipHintStyle.Default)
            {
                _messageTable.UpdateMessages(_randomized.GossipQuotes);
            }
        }


        private void WriteFileSelect()
        {
            ResourceUtils.ApplyHack(Values.ModsDirectory + "file-select");
            byte[] SkyboxDefault = new byte[] { 0x91, 0x78, 0x9B, 0x28, 0x00, 0x28 };
            List<int[]> Addrs = ResourceUtils.GetAddresses(Values.AddrsDirectory + "skybox-init");
            Random R = new Random();
            int rot = R.Next(360);
            for (int i = 0; i < 2; i++)
            {
                Color c = Color.FromArgb(SkyboxDefault[i * 3], SkyboxDefault[i * 3 + 1], SkyboxDefault[i * 3 + 2]);
                float h = c.GetHue();
                h += rot;
                h %= 360f;
                c = ColorUtils.FromAHSB(c.A, h, c.GetSaturation(), c.GetBrightness());
                SkyboxDefault[i * 3] = c.R;
                SkyboxDefault[i * 3 + 1] = c.G;
                SkyboxDefault[i * 3 + 2] = c.B;
            }

            for (int i = 0; i < 3; i++)
            {
                ReadWriteUtils.WriteROMAddr(Addrs[i], new byte[] { SkyboxDefault[i * 2], SkyboxDefault[i * 2 + 1] });
            }

            rot = R.Next(360);
            byte[] FSDefault = new byte[] { 0x64, 0x96, 0xFF, 0x96, 0xFF, 0xFF, 0x64, 0xFF, 0xFF };
            Addrs = ResourceUtils.GetAddresses(Values.AddrsDirectory + "fs-colour");
            for (int i = 0; i < 3; i++)
            {
                Color c = Color.FromArgb(FSDefault[i * 3], FSDefault[i * 3 + 1], FSDefault[i * 3 + 2]);
                float h = c.GetHue();
                h += rot;
                h %= 360f;
                c = ColorUtils.FromAHSB(c.A, h, c.GetSaturation(), c.GetBrightness());
                FSDefault[i * 3] = c.R;
                FSDefault[i * 3 + 1] = c.G;
                FSDefault[i * 3 + 2] = c.B;
            }
            for (int i = 0; i < 9; i++)
            {
                if (i < 6)
                {
                    ReadWriteUtils.WriteROMAddr(Addrs[i], new byte[] { 0x00, FSDefault[i] });
                }
                else
                {
                    ReadWriteUtils.WriteROMAddr(Addrs[i], new byte[] { FSDefault[i] });
                }
            }
        }

        private void WriteStartupStrings()
        {
            if (_settings.LogicMode == LogicMode.Vanilla)
            {
                //ResourceUtils.ApplyHack(ModsDir + "postman-testing");
                return;
            }
            Version v = Assembly.GetExecutingAssembly().GetName().Version;
            RomUtils.SetStrings(Values.ModsDirectory + "logo-text", $"v{v}", _settings.ToString());
        }

        private void WriteShopObjects()
        {
            RomUtils.CheckCompressed(1325); // trading post
            var data = RomData.MMFileList[1325].Data.ToList();
            data.RemoveRange(0x15C, 4); // reduce end padding from actors list
            data.InsertRange(0x62, new byte[] { 0x00, 0xC1, 0x00, 0xAF }); // add extra objects
            data[0x29] += 2; // increase object count by 2
            data[0x37] += 4; // add 4 to actor list address
            RomData.MMFileList[1325].Data = data.ToArray();

            RomUtils.CheckCompressed(1503); // bomb shop
            RomData.MMFileList[1503].Data[0x53] = 0x98; // add extra objects
            RomData.MMFileList[1503].Data[0x29] += 1; // increase object count by 1

            RomUtils.CheckCompressed(1142); // witch shop
            data = RomData.MMFileList[1142].Data.ToList();
            data.RemoveRange(0x78, 4); // reduce end padding from actors list
            data.InsertRange(0x48, new byte[] { 0x00, 0xC1, 0x00, 0xC1 }); // add extra objects
            data[0x29] += 2; // increase object count by 2
            data[0x37] += 4; // add 4 to actor list address
            RomData.MMFileList[1142].Data = data.ToArray();
        }

        private void WriteAsmPatch()
        {
            // Load patcher from internal resource file
            var patcher = Patcher.Load();
            var options = _settings.PatcherOptions;

            // Load the symbols and use them to apply the patch data
            patcher.Apply(options);
        }

        public void MakeROM(string InFile, string FileName, BackgroundWorker worker)
        {
            using (BinaryReader OldROM = new BinaryReader(File.Open(InFile, FileMode.Open, FileAccess.Read)))
            {
                RomUtils.ReadFileTable(OldROM);
                _messageTable.InitializeTable();
            }

            var originalMMFileList = RomData.MMFileList.Select(file => file.Clone()).ToList();

            byte[] hash;
            if (!string.IsNullOrWhiteSpace(_settings.InputPatchFilename))
            {
                worker.ReportProgress(50, "Applying patch...");
                hash = RomUtils.ApplyPatch(_settings.InputPatchFilename);
            }
            else
            {
                worker.ReportProgress(55, "Writing player model...");
                WritePlayerModel();

                if (_settings.LogicMode != LogicMode.Vanilla)
                {
                    worker.ReportProgress(60, "Applying hacks...");
                    ResourceUtils.ApplyHack(Values.ModsDirectory + "title-screen");
                    ResourceUtils.ApplyHack(Values.ModsDirectory + "misc-changes");
                    ResourceUtils.ApplyHack(Values.ModsDirectory + "cm-cs");
                    ResourceUtils.ApplyHack(Values.ModsDirectory + "fix-song-of-healing");
                    WriteFileSelect();
                }
                ResourceUtils.ApplyHack(Values.ModsDirectory + "init-file");
                ResourceUtils.ApplyHack(Values.ModsDirectory + "fierce-deity-anywhere");

                worker.ReportProgress(61, "Writing quick text...");
                WriteQuickText();

                worker.ReportProgress(62, "Writing cutscenes...");
                WriteCutscenes();

                worker.ReportProgress(63, "Writing dungeons...");
                WriteDungeons();

                worker.ReportProgress(64, "Writing gimmicks...");
                WriteGimmicks();

                worker.ReportProgress(65, "Writing speedups...");
                WriteSpeedUps();

                worker.ReportProgress(66, "Writing enemies...");
                WriteEnemies();

                // if shop should match given items
                {
                    WriteShopObjects();
                }

                worker.ReportProgress(67, "Writing items...");
                WriteItems();

                worker.ReportProgress(68, "Writing messages...");
                WriteGossipQuotes();

                MessageTable.WriteMessageTable(_messageTable);

                worker.ReportProgress(69, "Writing startup...");
                WriteStartupStrings();

                worker.ReportProgress(70, "Writing ASM patch...");
                WriteAsmPatch();
                
                worker.ReportProgress(71, _settings.GeneratePatch ? "Generating patch..." : "Computing hash...");
                hash = RomUtils.CreatePatch(_settings.GeneratePatch ? FileName : null, originalMMFileList);
            }

            worker.ReportProgress(72, "Writing cosmetics...");
            WriteTatlColour();
            WriteTunicColor();

            worker.ReportProgress(50, "Writing music...");
            WriteAudioSeq(new Random(BitConverter.ToInt32(hash, 0)));
            WriteMuteMusic();

            worker.ReportProgress(67, "Writing sound effects...");
            WriteSoundEffects(new Random(BitConverter.ToInt32(hash, 0)));

            if (_settings.GenerateROM)
            {
                worker.ReportProgress(75, "Building ROM...");

                byte[] ROM = RomUtils.BuildROM(FileName);
                if (_settings.OutputVC)
                {
                    worker.ReportProgress(90, "Building VC...");
                    VCInjectionUtils.BuildVC(ROM, Values.VCDirectory, Path.ChangeExtension(FileName, "wad"));
                }
            }
            worker.ReportProgress(100, "Done!");

        }

    }

}