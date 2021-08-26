﻿using RT.Common;
using Server.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace RT.Models.Lobby
{
    [MediusMessage(NetMessageTypes.MessageClassLobby, MediusLobbyMessageIds.GetMyClanMessages)]
    public class MediusGetMyClanMessagesRequest : BaseLobbyMessage, IMediusRequest
    {
        public override byte PacketType => (byte)MediusLobbyMessageIds.GetMyClanMessages;



        public MessageId MessageID { get; set; }
        public string SessionKey; // SESSIONKEY_MAXLEN
        public int ClanID;
        public int Start;
        public int PageSize;

        public override void Deserialize(BinaryReader reader)
        {
            // 
            base.Deserialize(reader);

            // 
            MessageID = reader.Read<MessageId>();
            SessionKey = reader.ReadString(Constants.SESSIONKEY_MAXLEN);
            ClanID = reader.ReadInt32();
            Start = reader.ReadInt32();
            PageSize = reader.ReadInt32();
        }

        public override void Serialize(BinaryWriter writer)
        {
            // 
            base.Serialize(writer);

            // 
            writer.Write(MessageID ?? MessageId.Empty);
            writer.Write(SessionKey, Constants.SESSIONKEY_MAXLEN);
            writer.Write(ClanID);
            writer.Write(Start);
            writer.Write(PageSize);
        }


        public override string ToString()
        {
            return base.ToString() + " " +
                $"MessageID:{MessageID}" + " " +
                $"SessionKey:{SessionKey}" + " " +
                $"ClanID:{ClanID}" + " " +
                $"Start:{Start}" + " " +
                $"PageSize:{PageSize}";
        }
    }
}