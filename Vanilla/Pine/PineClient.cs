using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;

namespace P3FESTrainer.Pine
{
    public class PineException : Exception
    {
        public PineException(string message) : base(message) { }
    }

    /// <summary>
    /// IPC client for PCSX2 PINE server protocol.
    /// </summary>
    public class PineClient : IDisposable
    {
        private const byte MsgRead8 = 0, MsgRead16 = 1, MsgRead32 = 2, MsgRead64 = 3;
        private const byte MsgWrite8 = 4, MsgWrite16 = 5, MsgWrite32 = 6, MsgWrite64 = 7;

        public const int DefaultSlot = 28011;

        private readonly int _slot;
        private readonly string? _unixPath;
        private Socket? _sock;

        public bool Connected => _sock != null && _sock.Connected;

        public PineClient(int slot = DefaultSlot, string? unixPath = null)
        {
            _slot = slot;
            _unixPath = unixPath;
        }

        public void Connect(int timeoutMs = 2000)
        {
            Close();

            Socket s;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                s = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                var result = s.BeginConnect(IPAddress.Loopback, _slot, null, null);
                if (!result.AsyncWaitHandle.WaitOne(timeoutMs))
                {
                    s.Close();
                    throw new PineException("Connection timed out.");
                }
                s.EndConnect(result);
            }
            else
            {
                string path = _unixPath ?? $"/tmp/pcsx2.sock.{_slot}";
                s = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
                s.Connect(new UnixDomainSocketEndPoint(path));
            }
            s.ReceiveTimeout = 5000;
            s.SendTimeout = 5000;
            _sock = s;
        }

        public void Close()
        {
            _sock?.Close();
            _sock = null;
        }

        public void Dispose() => Close();

        private byte[] SendRecv(byte[] body)
        {
            if (_sock == null) throw new PineException("Not connected.");
            var sizePrefix = BitConverter.GetBytes((uint)(body.Length + 4));
            var msg = new byte[sizePrefix.Length + body.Length];
            Buffer.BlockCopy(sizePrefix, 0, msg, 0, sizePrefix.Length);
            Buffer.BlockCopy(body, 0, msg, sizePrefix.Length, body.Length);
            try
            {
                _sock.Send(msg);
            }
            catch (Exception ex) when (ex is SocketException or ObjectDisposedException)
            {
                Close();
                throw new PineException("Connection to PCSX2 lost.");
            }

            byte[] header = RecvExact(4);
            uint total = BitConverter.ToUInt32(header, 0);
            byte[] rest = RecvExact((int)(total - 4));
            byte status = rest[0];
            if (status == 0xFF)
                throw new PineException("PCSX2 rejected the request.");
            byte[] payload = new byte[rest.Length - 1];
            Buffer.BlockCopy(rest, 1, payload, 0, payload.Length);
            return payload;
        }

        private byte[] RecvExact(int n)
        {
            var buf = new byte[n];
            int offset = 0;
            while (offset < n)
            {
                int read;
                try
                {
                    read = _sock!.Receive(buf, offset, n - offset, SocketFlags.None);
                }
                catch (Exception ex) when (ex is SocketException or ObjectDisposedException)
                {
                    Close();
                    throw new PineException("Connection to PCSX2 lost.");
                }
                if (read == 0)
                {
                    Close();
                    throw new PineException("Connection closed by PCSX2.");
                }
                offset += read;
            }
            return buf;
        }

        // ---- Single memory read/write ops ----
        public byte Read8(uint address) => SendRecv(BuildRead(MsgRead8, address))[0];
        public ushort Read16(uint address) => BitConverter.ToUInt16(SendRecv(BuildRead(MsgRead16, address)), 0);
        public uint Read32(uint address) => BitConverter.ToUInt32(SendRecv(BuildRead(MsgRead32, address)), 0);
        public ulong Read64(uint address) => BitConverter.ToUInt64(SendRecv(BuildRead(MsgRead64, address)), 0);

        public void Write8(uint address, byte value) => SendRecv(BuildWrite(MsgWrite8, address, new[] { value }));
        public void Write16(uint address, ushort value) => SendRecv(BuildWrite(MsgWrite16, address, BitConverter.GetBytes(value)));
        public void Write32(uint address, uint value) => SendRecv(BuildWrite(MsgWrite32, address, BitConverter.GetBytes(value)));
        public void Write64(uint address, ulong value) => SendRecv(BuildWrite(MsgWrite64, address, BitConverter.GetBytes(value)));

        private static byte[] BuildRead(byte opcode, uint address)
        {
            var buf = new byte[5];
            buf[0] = opcode;
            BitConverter.GetBytes(address).CopyTo(buf, 1);
            return buf;
        }

        private static byte[] BuildWrite(byte opcode, uint address, byte[] payload)
        {
            var buf = new byte[5 + payload.Length];
            buf[0] = opcode;
            BitConverter.GetBytes(address).CopyTo(buf, 1);
            payload.CopyTo(buf, 5);
            return buf;
        }

        public PineBatch Batch() => new PineBatch(this);

        public bool Ping()
        {
            try { Read8(0x00100000); return true; }
            catch (PineException) { return false; }
        }

        internal byte[] SendRecvInternal(byte[] body) => SendRecv(body);
    }

    public class PineBatch
    {
        private enum OpKind { Read, Write }
        private readonly PineClient _client;
        private readonly List<byte> _body = new();
        private readonly List<(OpKind kind, int size)> _ops = new();

        internal PineBatch(PineClient client) => _client = client;

        public int Count => _ops.Count;

        public PineBatch Read8(uint address) => AddRead(0, address, 1);
        public PineBatch Read16(uint address) => AddRead(1, address, 2);
        public PineBatch Read32(uint address) => AddRead(2, address, 4);
        public PineBatch Read64(uint address) => AddRead(3, address, 8);

        public PineBatch Write8(uint address, byte value) => AddWrite(4, address, new[] { value });
        public PineBatch Write16(uint address, ushort value) => AddWrite(5, address, BitConverter.GetBytes(value));
        public PineBatch Write32(uint address, uint value) => AddWrite(6, address, BitConverter.GetBytes(value));
        public PineBatch Write64(uint address, ulong value) => AddWrite(7, address, BitConverter.GetBytes(value));

        private PineBatch AddRead(byte opcode, uint address, int size)
        {
            _body.Add(opcode);
            _body.AddRange(BitConverter.GetBytes(address));
            _ops.Add((OpKind.Read, size));
            return this;
        }

        private PineBatch AddWrite(byte opcode, uint address, byte[] payload)
        {
            _body.Add(opcode);
            _body.AddRange(BitConverter.GetBytes(address));
            _body.AddRange(payload);
            _ops.Add((OpKind.Write, payload.Length));
            return this;
        }

        public List<object?> Execute()
        {
            var results = new List<object?>();
            if (_ops.Count == 0) return results;
            byte[] data = _client.SendRecvInternal(_body.ToArray());
            int offset = 0;
            foreach (var (kind, size) in _ops)
            {
                if (kind == OpKind.Write) { results.Add(null); continue; }
                object val = size switch
                {
                    1 => data[offset],
                    2 => BitConverter.ToUInt16(data, offset),
                    4 => BitConverter.ToUInt32(data, offset),
                    8 => BitConverter.ToUInt64(data, offset),
                    _ => throw new InvalidOperationException(),
                };
                results.Add(val);
                offset += size;
            }
            return results;
        }
    }
}
