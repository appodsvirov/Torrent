﻿using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using CNLab4;
using System.Collections;
using CNLab4.Messages.Server.Requests;
using CNLab4.Messages.Server;
using CNLab4.Messages.Server.Responses;
using CNLab4.Messages;

namespace CNLab4_Client
{
    class ErrorResponseException : Exception
    {
        public ErrorResponseException() { }
        public ErrorResponseException(string msg) : base(msg) { }
    }

    class UnknownResponseException : Exception { }

    static class ServerProtocol
    {
        /// <exception cref="ErrorResponseException"></exception>
        /// <exception cref="UnknownResponseException"></exception>
        public static async Task<TorrentInfo> RegisterTorrentDirAsync(string dir)
        {
            DirectoryInfo dirInfo = new DirectoryInfo(dir);
            string rootDirName = dirInfo.Name;
            string baseDir = dirInfo.Parent.FullName;

            // collects files info
            List<string> relativePaths = DirectoryEx.GetRelativeFilesPaths(dir);
            for (int i = 0; i < relativePaths.Count; ++i)
                relativePaths[i] = Path.Combine(rootDirName, relativePaths[i]);

            List<RegisterTorrent.FileInfo> filesInfo = new List<RegisterTorrent.FileInfo>();
            foreach (string relativePath in relativePaths)
            {
                string path = Path.Combine(baseDir, relativePath);
                filesInfo.Add(new RegisterTorrent.FileInfo
                {
                    RelativePath = relativePath,
                    Length = new FileInfo(path).Length
                });
            }

            return await RegisterTorrentAsync(rootDirName, filesInfo);
        }

        /// <exception cref="ErrorResponseException"></exception>
        /// <exception cref="UnknownResponseException"></exception>
        public static async Task<TorrentInfo> RegisterTorrentFileAsync(string filePath)
        {
            string relativePath = Path.GetFileName(filePath);
            List<RegisterTorrent.FileInfo> filesInfo = new List<RegisterTorrent.FileInfo>();
            filesInfo.Add(new RegisterTorrent.FileInfo
            {
                RelativePath = relativePath,
                Length = new FileInfo(filePath).Length
            });

            return await RegisterTorrentAsync(relativePath, filesInfo);
        }

        /// <exception cref="ErrorResponseException"></exception>
        /// <exception cref="UnknownResponseException"></exception>
        private static async Task<TorrentInfo> RegisterTorrentAsync(string torrentName,
            IList<RegisterTorrent.FileInfo> filesInfo)
        {
            using (TcpClient client = new TcpClient(AddressFamily.InterNetwork))
            {
                await client.ConnectAsync(General.ServerAddress.Address, General.ServerAddress.Port);
                NetworkStream stream = client.GetStream();

                // request
                BaseServerRequest request = new RegisterTorrent
                {
                    SenderAddress = General.PeerAddress,
                    TorrentName = torrentName,
                    FilesInfo = filesInfo
                };
                await stream.WriteAsync(request);

                // response
                BaseServerResponse response = await stream.ReadMessageAsync<BaseServerResponse>();
                if (response is TorrentRegistered niceResponse)
                {
                    TorrentFileInfo[] torrentFileInfos = new TorrentFileInfo[filesInfo.Count];
                    for (int i = 0; i < filesInfo.Count; ++i)
                        torrentFileInfos[i] = new TorrentFileInfo(filesInfo[i].RelativePath,
                            filesInfo[i].Length, niceResponse.BlocksSizes[i]);
                    return new TorrentInfo(torrentName, torrentFileInfos, niceResponse.AccessCode);
                }
                else if (response is Error errorResponse)
                {
                    throw new ErrorResponseException(errorResponse.Text);
                }
                else
                    throw new UnknownResponseException();
            }
        }

        /// <exception cref="ErrorResponseException"></exception>
        /// <exception cref="UnknownResponseException"></exception>
        public static async Task<TorrentInfo> GetTorrentInfoAsync(string accessCode)
        {
            using (TcpClient client = new TcpClient(AddressFamily.InterNetwork))
            {
                await client.ConnectAsync(General.ServerAddress.Address, General.ServerAddress.Port);
                NetworkStream stream = client.GetStream();

                BaseServerRequest request = new GetTorrentInfo
                {
                    AccessCode = accessCode,
                    SenderAddress = General.PeerAddress
                };
                await stream.WriteAsync(request);

                BaseServerResponse response = await stream.ReadMessageAsync<BaseServerResponse>();
                if (response is TorrentInfoResponse torrentInfoResponse)
                {
                    return torrentInfoResponse.TorrentInfo;
                }
                else if (response is Error errorResponse)
                {
                    throw new ErrorResponseException(errorResponse.Text);
                }
                else
                    throw new UnknownResponseException();
            }
        }

        /// <exception cref="ErrorResponseException"></exception>
        /// <exception cref="UnknownResponseException"></exception>
        public static async Task<IList<IPEndPoint>> GetPeersAsync(string accessCode)
        {
            using (TcpClient client = new TcpClient(AddressFamily.InterNetwork))
            {
                await client.ConnectAsync(General.ServerAddress.Address, General.ServerAddress.Port);
                NetworkStream stream = client.GetStream();

                await stream.WriteAsync(new GetPeers
                {
                    AccessCode = accessCode
                });

                BaseServerResponse response = await stream.ReadMessageAsync<BaseServerResponse>();
                if (response is PeersResponse peersResponse)
                {
                    return peersResponse.Peers;
                }
                else if (response is Error errorResponse)
                {
                    throw new ErrorResponseException(errorResponse.Text);
                }
                else
                    throw new UnknownResponseException();
            }
        }

        public static Task<IList<IPEndPoint>> GetPeersTask(string accessCode)
        {
            return Task.Run(() => GetPeers(accessCode));
        }

        /// <exception cref="ErrorResponseException"></exception>
        /// <exception cref="UnknownResponseException"></exception>
        public static IList<IPEndPoint> GetPeers(string accessCode)
        {
            using (TcpClient client = new TcpClient(AddressFamily.InterNetwork))
            {
                client.Connect(General.ServerAddress);
                NetworkStream stream = client.GetStream();

                stream.Write(new GetPeers
                {
                    AccessCode = accessCode
                });

                BaseServerResponse response = stream.ReadMessage<BaseServerResponse>();
                if (response is PeersResponse peersResponse)
                {
                    return peersResponse.Peers;
                }
                else if (response is Error errorResponse)
                {
                    throw new ErrorResponseException(errorResponse.Text);
                }
                else
                    throw new UnknownResponseException();
            }
        }
    }
}
