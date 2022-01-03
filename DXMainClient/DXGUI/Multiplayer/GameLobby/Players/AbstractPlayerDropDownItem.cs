using System;
using DTAClient.Domain.Multiplayer;

namespace DTAClient.DXGUI.Multiplayer.GameLobby.Players
{
    public abstract class AbstractPlayerDropDownItem
    {
        public PlayerInfo PlayerInfo { get; set; }
        public PlayerDropDownItemTypeEnum Type { get; set; }
        
        public Action SelectAction { get; set; }

        public AbstractPlayerDropDownItem(PlayerDropDownItemTypeEnum type)
        {
            Type = type;
        }

        public bool IsHost => Type == PlayerDropDownItemTypeEnum.Host;
        public bool IsUnused => Type == PlayerDropDownItemTypeEnum.Unused;
        public bool IsHuman => Type == PlayerDropDownItemTypeEnum.Human;
        public bool IsAI => Type == PlayerDropDownItemTypeEnum.AI;
        public bool IsInvite => Type == PlayerDropDownItemTypeEnum.Invite;
    }
}
