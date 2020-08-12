using Deadlocked.Server.Stream;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Deadlocked.Server.Messages.Lobby
{
    [MediusApp(MediusAppPacketIds.GetLocationsResponse)]
    public class MediusGetLocationsResponse : BaseLobbyMessage
    {

        public override MediusAppPacketIds Id => MediusAppPacketIds.GetLocationsResponse;

        public int LocationID;
        public string LocationName; // LOCATIONNAME_MAXLEN
        public MediusCallbackStatus StatusCode;
        public bool EndOfList;

        public override void Deserialize(BinaryReader reader)
        {
            // 
            base.Deserialize(reader);

            // 
            reader.ReadBytes(3);
            LocationID = reader.ReadInt32();
            LocationName = reader.ReadString(MediusConstants.LOCATIONNAME_MAXLEN);
            StatusCode = reader.Read<MediusCallbackStatus>();
            EndOfList = reader.ReadBoolean();
            reader.ReadBytes(3);
        }

        public override void Serialize(BinaryWriter writer)
        {
            // 
            base.Serialize(writer);

            // 
            writer.Write(new byte[3]);
            writer.Write(LocationID);
            writer.Write(LocationName, MediusConstants.LOCATIONNAME_MAXLEN);
            writer.Write(StatusCode);
            writer.Write(EndOfList);
            writer.Write(new byte[3]);
        }


        public override string ToString()
        {
            return base.ToString() + " " +
             $"LocationID:{LocationID}" + " " +
$"LocationName:{LocationName}" + " " +
$"StatusCode:{StatusCode}" + " " +
$"EndOfList:{EndOfList}";
        }
    }
}