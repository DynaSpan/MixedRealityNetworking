using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace MixedRealityNetworking
{
    public class SocketClientManager
    {
        #region Fields

        /// <summary>
        /// The ip address the client connects to
        /// </summary>
        private static string host;

        /// <summary>
        /// The port the client connects to
        /// </summary>
        private static int port;

        /// <summary>
        /// Contains the callbackmethods for seperate message ID's
        /// </summary>
        private static Dictionary<byte, Action<NetworkMessage>> callbackMethods = new Dictionary<byte, Action<NetworkMessage>>();

        /// <summary>
        /// The thread on which the socket runs
        /// </summary>
        private static Thread socketThread;

        /// <summary>
        /// The UDP client
        /// </summary>
        private static UdpClient udpClient;

        /// <summary>
        /// Making sure the thread aborts
        /// </summary>
        private static bool abortThread = false;

        /// <summary>
        /// Boolean that indicates if we should print debug information
        /// </summary>
        private static bool verboseMode = false;

        #endregion

        #region Properties

        /// <summary>
        /// The host to which the socket needs to connect
        /// </summary>
        public static string Host
        {
            set { SocketClientManager.host = value; }
        }

        /// <summary>
        /// The port to which the socket needs to connect
        /// </summary>
        public static int Port
        {
            set { SocketClientManager.port = value; }
        }

        /// <summary>
        /// Boolean indicating if debug info should be printed
        /// </summary>
        public static bool VerboseMode
        {
            get { return SocketClientManager.verboseMode; }
            set { SocketClientManager.verboseMode = value; }
        }

        #endregion

        #region Methods

        /// <summary>
        /// Connects to the <c cref="udpClient">socket</c> and starts listening
        /// </summary>
        public static void Connect()
        {
            SocketClientManager.udpClient = udpClient = new UdpClient(SocketClientManager.host, SocketClientManager.port);

            SocketClientManager.socketThread = new Thread(SocketClientManager.Listen);
            SocketClientManager.socketThread.Start();
        }

        /// <summary>
        /// Method to subscribe to a message
        /// </summary>
        /// <exception cref="InvalidOperationException">Gets thrown when there is already a subscription for the message ID</exception>
        /// <param name="messageId">The id of the message you want to subscribe to</param>
        /// <param name="callbackMethod">The method that should be called</param>
        public static void Subscribe(byte messageId, Action<NetworkMessage> callbackMethod)
        {
            // Check if not already subscribed
            if (SocketClientManager.callbackMethods.ContainsKey(messageId))
                throw new InvalidOperationException("There is already a subscription to this message ID");

            SocketClientManager.callbackMethods.Add(messageId, callbackMethod);
        }

        /// <summary>
        /// Sends a message
        /// </summary>
        /// <param name="nm">The <c cref="NetworkMessage">network message</c> that needs to be send</param>
        public static void SendMessage(NetworkMessage nm)
        {
            // Write the data into a byte array
            byte[] byteArray = new byte[nm.Content.Length + 1];

            byteArray[0] = nm.MessageId;

            // Write the content into the array
            var i = 1;

            foreach (byte messageData in nm.Content)
            {
                byteArray[i] = messageData;
                ++i;
            }

            PrintDebug("Sending message");

            // Send it
            udpClient.Send(byteArray, byteArray.Length);
        }

        /// <summary>
        /// Method that listens for incoming messages on the <c cref="udpClient">socket</c>
        /// </summary>
        private static void Listen()
        {
            try
            {
                IPEndPoint endpoint = new IPEndPoint(IPAddress.Any, 0);

                // Keep looping as long as the thread isn't aborted
                while (!SocketClientManager.abortThread)
                {
                    byte[] clientData = udpClient.Receive(ref endpoint);

                    // Data received, create a new NetworkMessage
                    byte messageId = clientData[0];

                    // Remove the message ID from the data
                    byte[] message = new byte[clientData.Length - 1];

                    for (int i = 1; i < clientData.Length; ++i)
                    {
                        message[i - 1] = clientData[i];
                    }

                    // Call the correct callback method
                    if (SocketClientManager.callbackMethods.ContainsKey(messageId))
                    {
                        // Catch any exceptions and rethrow them,
                        // so a user gets a good exception instead of a 
                        // object disposed exception
                        try
                        {
                            SocketClientManager.callbackMethods[messageId](new NetworkMessage(messageId, message));
                        }
                        catch (Exception e)
                        {
                            throw e;
                        }
                    }
                    else
                    {
                        PrintDebug("No known callback for message ID " + messageId.ToString());
                    }
                }
            }
            catch (ThreadAbortException e)
            {
                // Thread aborted, do nothing
            }
            catch (SocketException e)
            {
                // Socket aborted, probably because we want to close it
                if (e.ErrorCode != 10004)
                    throw e;
            }
            catch (Exception e)
            {
                // Rethrow exception
                throw e;

                PrintDebug(e.Message);
            }
            finally
            {
                // Make sure we always close the connection
                udpClient.Close();
            }
        }

        /// <summary>
        /// Stops listening to the <c cref="udpClient">socket</c> and closes the <c cref="socketThread">thread</c>
        /// </summary>
        public static void StopListening()
        {
            // Close the UDP client, since aborting the thread
            // doesn't work if it's waiting for packets
            if (udpClient != null)
                udpClient.Close();

            // Check if the thread is initialized
            if (SocketClientManager.socketThread is Thread && SocketClientManager.socketThread != null)
            {
                SocketClientManager.abortThread = true;

                //SocketClientManager.socketThread.Abort();
            }
        }

        /// <summary>
        /// Method that prints debug information when verbose mode is enabled
        /// </summary>
        /// <param name="message">The message that needs to be printed</param>
        private static void PrintDebug(string message)
        {
            if (SocketClientManager.verboseMode)
                Trace.Write(message);
        }

        #endregion
    }
}
