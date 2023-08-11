using System;
using System.Runtime.InteropServices;

namespace Andraste.Payload.Native
{
    public class WS2_32
    {
        [DllImport("ws2_32.dll", CharSet = CharSet.Ansi)]
        public static extern IntPtr gethostbyname(string name);
        public delegate IntPtr Delegate_gethostbyname(string name);
        
        [StructLayout(LayoutKind.Sequential)]
        public struct SockAddr
        {
            public short Family;
            public ushort Port;
            public AddressIP4 IPAddress;
            private Int64 Zero;

            public SockAddr (short Family, ushort Port, AddressIP4 IP)
            { this.Family = Family; this.Port = Port; this.IPAddress = IP; this.Zero = 0; }
        }
        
        [StructLayout(LayoutKind.Sequential)]
        public struct AddressIP4
        {
            public byte a1;
            public byte a2;
            public byte a3;
            public byte a4;
            
            public static AddressIP4 Broadcast { get { return new AddressIP4 (255,255,255,255); } }
            public static AddressIP4 AnyAddress { get { return new AddressIP4 (0,0,0,0); } }
            public static AddressIP4 Loopback { get { return new AddressIP4 (127,0,0,1); } }

            public AddressIP4 (byte a1, byte a2, byte a3, byte a4)
            { this.a1 = a1; this.a2 = a2; this.a3 = a3; this.a4 = a4; }
        }
        
        [DllImport("ws2_32.dll", CharSet = CharSet.Ansi)]
        public static extern IntPtr sendto(IntPtr socket, IntPtr buf, int len, int flag, ref SockAddr to, int tolen);  
        public delegate IntPtr Delegate_sendto(IntPtr socket, IntPtr buf, int len, int flag, ref SockAddr to, int tolen);
        
        [DllImport("ws2_32.dll", CharSet = CharSet.Ansi)]
        public static extern IntPtr recvfrom(IntPtr socket, IntPtr buf, int len, int flags, out SockAddr from, int fromlen);
        public delegate IntPtr Delegate_recvfrom(IntPtr socket, IntPtr buf, int len, int flag, out SockAddr from, int fromlen);
    }
}
