﻿//  __  _  __    __   ___ __  ___ ___
// |  \| |/__\ /' _/ / _//__\| _ \ __|
// | | ' | \/ |`._`.| \_| \/ | v / _|
// |_|\__|\__/ |___/ \__/\__/|_|_\___|
// 
// Copyright (C) 2019 - NosCore
// 
// NosCore is a free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or any later version.
// 
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with this program.  If not, see <http://www.gnu.org/licenses/>.

using System;
using System.Threading.Tasks;
using NosCore.Packets.ClientPackets.Specialists;
using NosCore.Packets.Enumerations;
using NosCore.Packets.ServerPackets.UI;
using NosCore.Core;
using NosCore.Core.I18N;
using NosCore.Data.Enumerations;
using NosCore.Data.Enumerations.I18N;
using NosCore.GameObject;
using NosCore.GameObject.Networking.ClientSession;
using NosCore.GameObject.Networking.Group;
using NosCore.GameObject.Providers.ItemProvider.Item;

namespace NosCore.PacketHandlers.Inventory
{
    public class SpTransformPacketHandler : PacketHandler<SpTransformPacket>, IWorldPacketHandler
    {
        public override async Task ExecuteAsync(SpTransformPacket spTransformPacket, ClientSession clientSession)
        {
            if (spTransformPacket.Type == SlPacketType.ChangePoints)
            {
                //TODO set points
            }
            else
            {
                if (clientSession.Character.IsSitting)
                {
                    return;
                }

                if (!(clientSession.Character.InventoryService.LoadBySlotAndType((byte)EquipmentType.Sp,
                    NoscorePocketType.Wear)?.ItemInstance is SpecialistInstance specialistInstance))
                {
                    await clientSession.SendPacketAsync(new MsgPacket
                    {
                        Message = GameLanguage.Instance.GetMessageFromKey(LanguageKey.NO_SP, clientSession.Account.Language)
                    }).ConfigureAwait(false);

                    return;
                }

                if (clientSession.Character.IsVehicled)
                {
                    await clientSession.SendPacketAsync(new MsgiPacket
                    {
                        Message = Game18NConstString.CantUseInVehicle
                    }).ConfigureAwait(false);
                    return;
                }

                var currentRunningSeconds = (SystemTime.Now() - clientSession.Character.LastSp).TotalSeconds;

                if (clientSession.Character.UseSp)
                {
                    clientSession.Character.LastSp = SystemTime.Now();
                    await clientSession.Character.RemoveSpAsync().ConfigureAwait(false);
                }
                else
                {
                    if ((clientSession.Character.SpPoint == 0) && (clientSession.Character.SpAdditionPoint == 0))
                    {
                        await clientSession.SendPacketAsync(new MsgPacket
                        {
                            Message = GameLanguage.Instance.GetMessageFromKey(LanguageKey.SP_NOPOINTS,
                                clientSession.Account.Language)
                        }).ConfigureAwait(false);
                        return;
                    }

                    if (currentRunningSeconds >= clientSession.Character.SpCooldown)
                    {
                        if (spTransformPacket.Type == SlPacketType.WearSpAndTransform)
                        {
                            await clientSession.Character.ChangeSpAsync().ConfigureAwait(false);
                        }
                        else
                        {
                            await clientSession.SendPacketAsync(new DelayPacket
                            {
                                Type = 3,
                                Delay = 5000,
                                Packet = new SpTransformPacket { Type = SlPacketType.WearSp }
                            }).ConfigureAwait(false);
                            await clientSession.Character.MapInstance.SendPacketAsync(new GuriPacket
                            {
                                Type = GuriPacketType.Unknow,
                                Value = 1,
                                EntityId = clientSession.Character.CharacterId
                            }).ConfigureAwait(false);
                        }
                    }
                    else
                    {
                        await clientSession.SendPacketAsync(new MsgPacket
                        {
                            Message = string.Format(GameLanguage.Instance.GetMessageFromKey(LanguageKey.SP_INLOADING,
                                    clientSession.Account.Language),
                                clientSession.Character.SpCooldown - (int)Math.Round(currentRunningSeconds))
                        }).ConfigureAwait(false);
                    }
                }
            }
        }
    }
}