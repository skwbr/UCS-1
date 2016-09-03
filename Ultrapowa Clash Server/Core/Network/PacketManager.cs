/*
 * Program : Ultrapowa Clash Server
 * Description : A C# Writted 'Clash of Clans' Server Emulator !
 *
 * Authors:  Jean-Baptiste Martin <Ultrapowa at Ultrapowa.com>,
 *           And the Official Ultrapowa Developement Team
 *
 * Copyright (c) 2016  UltraPowa
 * All Rights Reserved.
 */

using System;
using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Threading;
using UCS.Logic;
using UCS.PacketProcessing;

namespace UCS.Core.Network
{
    internal class PacketManager : IDisposable
    {
        private static readonly EventWaitHandle m_vIncomingWaitHandle = (EventWaitHandle)new AutoResetEvent(false);
        private static readonly EventWaitHandle m_vOutgoingWaitHandle = (EventWaitHandle)new AutoResetEvent(false);
        private static ConcurrentQueue<Message> m_vIncomingPackets;
        private static ConcurrentQueue<Message> m_vOutgoingPackets;
        private bool m_vIsRunning;

        public PacketManager()
        {
            PacketManager.m_vIncomingPackets = new ConcurrentQueue<Message>();
            PacketManager.m_vOutgoingPackets = new ConcurrentQueue<Message>();
            this.m_vIsRunning = false;
        }

        public void Dispose()
        {
            PacketManager.m_vIncomingWaitHandle.Dispose();
            GC.SuppressFinalize((object)this);
            PacketManager.m_vOutgoingWaitHandle.Dispose();
        }

        public static void ProcessIncomingPacket(Message p)
        {
            PacketManager.m_vIncomingPackets.Enqueue(p);
            PacketManager.m_vIncomingWaitHandle.Set();
        }

        public static void ProcessOutgoingPacket(Message p)
        {
            p.Encode();
            try
            {
                Level level = p.Client.GetLevel();
                string str = "";
                if (level != null)
                    str = " (" + (object)level.GetPlayerAvatar().GetId() + ", " + level.GetPlayerAvatar().GetAvatarName() + ")";
                PacketManager.m_vOutgoingPackets.Enqueue(p);
                PacketManager.m_vOutgoingWaitHandle.Set();
            }
            catch (Exception ex)
            {
            }
        }

        public void Start()
        {
            new PacketManager.IncomingProcessingDelegate(this.IncomingProcessing).BeginInvoke((AsyncCallback)null, (object)null);
            new PacketManager.OutgoingProcessingDelegate(this.OutgoingProcessing).BeginInvoke((AsyncCallback)null, (object)null);
            this.m_vIsRunning = true;
            Console.WriteLine("[UCS]    Packet Manager started successfully");
        }

        private void IncomingProcessing()
        {
            while (this.m_vIsRunning)
            {
                PacketManager.m_vIncomingWaitHandle.WaitOne();
                Message result;
                while (PacketManager.m_vIncomingPackets.TryDequeue(out result))
                {
                    result.GetData();
                    result.Decrypt();
                    MessageManager.ProcessPacket(result);
                }
            }
        }

        private void OutgoingProcessing()
        {
            while (this.m_vIsRunning)
            {
                PacketManager.m_vOutgoingWaitHandle.WaitOne();
                Message result;
                while (PacketManager.m_vOutgoingPackets.TryDequeue(out result))
                {
                    try
                    {
                        if (result.Client.Socket != null)
                            result.Client.Socket.Send(result.GetRawData());
                        else
                            ResourcesManager.DropClient(result.Client.GetSocketHandle());
                    }
                    catch (Exception ex1)
                    {
                        try
                        {
                            ResourcesManager.DropClient(result.Client.GetSocketHandle());
                            result.Client.Socket.Shutdown(SocketShutdown.Both);
                            result.Client.Socket.Close();
                        }
                        catch (Exception ex2)
                        {
                        }
                    }
                }
            }
        }

        private delegate void IncomingProcessingDelegate();

        private delegate void OutgoingProcessingDelegate();
    }
}
