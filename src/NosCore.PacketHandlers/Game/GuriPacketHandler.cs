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
using NosCore.Packets.ClientPackets.UI;
using NosCore.GameObject;
using NosCore.GameObject.Networking.ClientSession;
using NosCore.GameObject.Providers.GuriProvider;

namespace NosCore.PacketHandlers.Game
{
    public class GuriPacketHandler : PacketHandler<GuriPacket>, IWorldPacketHandler
    {
        private readonly IGuriProvider _guriProvider;

        public GuriPacketHandler(IGuriProvider guriProvider)
        {
            _guriProvider = guriProvider;
        }

        public override Task ExecuteAsync(GuriPacket guriPacket, ClientSession session)
        {
            _guriProvider.GuriLaunch(session, guriPacket);
            return Task.CompletedTask;
        }
    }
}