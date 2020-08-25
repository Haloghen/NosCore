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

using System.Threading.Tasks;
using NosCore.Packets.ClientPackets.Movement;
using NosCore.Packets.Enumerations;
using NosCore.Core.I18N;
using NosCore.Data.Enumerations.I18N;
using NosCore.GameObject;
using NosCore.GameObject.ComponentEntities.Extensions;
using NosCore.GameObject.ComponentEntities.Interfaces;
using NosCore.GameObject.Networking.ClientSession;
using Serilog;

namespace NosCore.PacketHandlers.Movement
{
    public class ClientDirPacketHandler : PacketHandler<ClientDirPacket>, IWorldPacketHandler
    {
        private readonly ILogger _logger;

        public ClientDirPacketHandler(ILogger logger)
        {
            _logger = logger;
        }

        public override Task ExecuteAsync(ClientDirPacket dirpacket, ClientSession session)
        {
            IAliveEntity entity;
            switch (dirpacket.VisualType)
            {
                case VisualType.Player:
                    entity = session.Character!;
                    break;
                default:
                    _logger.Error(LogLanguage.Instance.GetMessageFromKey(LogLanguageKey.VISUALTYPE_UNKNOWN),
                        dirpacket.VisualType);
                    return Task.CompletedTask;
            }

            return entity.ChangeDirAsync(dirpacket.Direction);
        }
    }
}