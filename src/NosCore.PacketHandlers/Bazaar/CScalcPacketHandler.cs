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
using NosCore.Core.Configuration;
using NosCore.Packets.ClientPackets.Bazaar;
using NosCore.Packets.Enumerations;
using NosCore.Packets.ServerPackets.Bazaar;
using NosCore.Packets.ServerPackets.UI;
using NosCore.Core.I18N;
using NosCore.Dao.Interfaces;
using NosCore.Data.Dto;
using NosCore.Data.Enumerations.I18N;
using NosCore.GameObject;
using NosCore.GameObject.ComponentEntities.Extensions;
using NosCore.GameObject.HttpClients.BazaarHttpClient;
using NosCore.GameObject.Networking.ClientSession;
using NosCore.GameObject.Providers.InventoryService;
using NosCore.GameObject.Providers.ItemProvider;
using Serilog;

namespace NosCore.PacketHandlers.Bazaar
{
    public class CScalcPacketHandler : PacketHandler<CScalcPacket>, IWorldPacketHandler
    {
        private readonly IBazaarHttpClient _bazaarHttpClient;
        private readonly IDao<IItemInstanceDto?, Guid> _itemInstanceDao;
        private readonly IItemProvider _itemProvider;
        private readonly ILogger _logger;
        private readonly WorldConfiguration _worldConfiguration;

        public CScalcPacketHandler(WorldConfiguration worldConfiguration, IBazaarHttpClient bazaarHttpClient,
            IItemProvider itemProvider, ILogger logger, IDao<IItemInstanceDto?, Guid> itemInstanceDao)
        {
            _worldConfiguration = worldConfiguration;
            _bazaarHttpClient = bazaarHttpClient;
            _itemProvider = itemProvider;
            _logger = logger;
            _itemInstanceDao = itemInstanceDao;
        }

        public override async Task ExecuteAsync(CScalcPacket packet, ClientSession clientSession)
        {
            var bz =  await _bazaarHttpClient.GetBazaarLinkAsync(packet.BazaarId).ConfigureAwait(false);
            if ((bz != null) && (bz.SellerName == clientSession.Character.Name))
            {
                var soldedamount = bz.BazaarItem!.Amount - bz.ItemInstance!.Amount;
                var taxes = bz.BazaarItem.MedalUsed ? (short) 0 : (short) (bz.BazaarItem.Price * 0.10 * soldedamount);
                var price = bz.BazaarItem.Price * soldedamount - taxes;
                if (clientSession.Character.InventoryService.CanAddItem(bz.ItemInstance.ItemVNum))
                {
                    if (clientSession.Character.Gold + price <= _worldConfiguration.MaxGoldAmount)
                    {
                        clientSession.Character.Gold += price;
                        await clientSession.SendPacketAsync(clientSession.Character.GenerateGold()).ConfigureAwait(false);
                        await clientSession.SendPacketAsync(clientSession.Character.GenerateSay(string.Format(
                            GameLanguage.Instance.GetMessageFromKey(LanguageKey.REMOVE_FROM_BAZAAR,
                                clientSession.Account.Language), price), SayColorType.Yellow)).ConfigureAwait(false);
                        var itemInstance = await _itemInstanceDao.FirstOrDefaultAsync(s => s!.Id == bz.ItemInstance.Id).ConfigureAwait(false);
                        if (itemInstance == null)
                        {
                            return;
                        }
                        var item = _itemProvider.Convert(itemInstance);
                        item.Id = Guid.NewGuid();

                        var newInv =
                            clientSession.Character.InventoryService.AddItemToPocket(
                                InventoryItemInstance.Create(item, clientSession.Character.CharacterId));
                        await clientSession.SendPacketAsync(newInv!.GeneratePocketChange()).ConfigureAwait(false);
                        var remove = await _bazaarHttpClient.RemoveAsync(packet.BazaarId, bz.ItemInstance.Amount,
                            clientSession.Character.Name).ConfigureAwait(false);
                        if (remove)
                        {
                            await clientSession.SendPacketAsync(new RCScalcPacket
                            {
                                Type = VisualType.Player,
                                Price = bz.BazaarItem.Price,
                                RemainingAmount = (short) (bz.BazaarItem.Amount - bz.ItemInstance.Amount),
                                Amount = bz.BazaarItem.Amount,
                                Taxes = taxes,
                                Total = price + taxes
                            }).ConfigureAwait(false);
                            await clientSession.HandlePacketsAsync(new[]
                                {new CSListPacket {Index = 0, Filter = BazaarStatusType.Default}}).ConfigureAwait(false);
                            return;
                        }

                        _logger.Error(LogLanguage.Instance.GetMessageFromKey(LogLanguageKey.BAZAAR_DELETE_ERROR));
                    }
                    else
                    {
                        await clientSession.SendPacketAsync(new MsgiPacket
                        {
                            Message = Game18NConstString.MaxGoldReached,
                            Type = MessageType.Whisper
                        }).ConfigureAwait(false);
                    }
                }
                else
                {
                    await clientSession.SendPacketAsync(new InfoiPacket
                    {
                        Message = Game18NConstString.NotEnoughSpace
                    }).ConfigureAwait(false);
                }

                await clientSession.SendPacketAsync(new RCScalcPacket
                {
                    Type = VisualType.Player, Price = bz.BazaarItem.Price, RemainingAmount = 0,
                    Amount = bz.BazaarItem.Amount, Taxes = 0, Total = 0
                }).ConfigureAwait(false);
            }
            else
            {
                await clientSession.SendPacketAsync(new RCScalcPacket
                    {Type = VisualType.Player, Price = 0, RemainingAmount = 0, Amount = 0, Taxes = 0, Total = 0}).ConfigureAwait(false);
            }
        }
    }
}