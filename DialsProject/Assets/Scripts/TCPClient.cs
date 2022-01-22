using System;
//using System.Collections;
//using System.Collections.Generic;
using System.Net.Sockets;
using System.Net;
//using System.Text;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;

using System.Collections;
//using UnityEngine.Android;
public class TCPClient : MonoBehaviour {

	//class which reads memory and sets values
	//have seperate for server and clietn so i can test on same pc
	
	public BuildControl buildControl;
	public AirplaneData iL2GameDataClient;

	public MenuHandler menuHandler;
	public RotateNeedle rN;

	//user settings		

	public bool connected = false;
	public float autoScanTimeScale = 1f;
	public float standardFixedTime = 0.02f;
	public bool autoScan = false;
	public bool hostFound;
	public bool tcpReceived = false;

	//user can insert from menu, if empty, autoscan happens
	public string userIP;
	//user can overwrite this
	public int portNumber = 11200;
	public bool waitingOnResponse;

	
	public float timerOfLastReceived = 0f;
	public bool testPrediction = false;

	#region private members 	
	private TcpClient socketConnection; 	
	

	public string hostName;
	public int ip4;
	public int ip3;

	public int socketTimeoutTime = 5;	
	public float timer = 5f;
	public float connectionTimer = 0f;

	public float handshakeInterval = 3f;
	public float handshakeTimer = 0f;

	bool localScanAttempted = false;//127.0.0.1 internal loopback scan

	#endregion
	UdpClient listener;// = new UdpClient(listenPort)

	IPEndPoint listenEndPoint;


	void Awake()
    {
		if (buildControl.isServer)
        {
			//disable client script
			enabled = false;
        }

		//so if statement fires on first frame
		handshakeTimer = handshakeInterval;
    }


    private void Start()
    {
		//will need to re run this on port change
		listener = new UdpClient(portNumber);
		listenEndPoint = new IPEndPoint(IPAddress.Any, portNumber);
		//start udp sender test
		//Thread threadListen = new Thread(() => UDPSender());
		//threadListen.IsBackground = true;
		//threadListen.Start();//does this close automatically?
		/*
		Thread threadListen = new Thread(() => UDPListener());
		threadListen.IsBackground = true;
		threadListen.Start();//does this close automatically?

		*/

		StartCoroutine("Listener");
	}
	public void Update()
	{
		//wait before scanning
		if (menuHandler.stopwatch.ElapsedMilliseconds < 5)
			return;


		if(hostFound)
        {
			if(!autoScan)
				menuHandler.scanDebug.GetComponent<Text>().text = "Connected: " + userIP.ToString() + " : " + portNumber; //causes exception - because of async?
			else
				menuHandler.scanDebug.GetComponent<Text>().text = "Connected: " + hostName.ToString() + " : " + portNumber; //causes exception
		}

		if (menuHandler.ipFieldOpen || menuHandler.portFieldOpen)
        {
			//wait til input is finished if scanning. cpu high 
			//Debug.Log("waiting on ip input");
			return;
        }

		//check if we should autoscan
		//if ipaddress is empty, then we should
		if (string.IsNullOrEmpty(userIP))
		{
			autoScan = true;
		}
		else
		{
			autoScan = false;
		}

		//set fixed time depending on whether we are connected or not. This governs how quickly we request data if connected, or how quickly we listen for data on new threads/sockets
		//setting up new threads and new sockets is cpu intensive, so this needs to be slower
		if (!connected)
			Time.fixedDeltaTime = autoScanTimeScale;
		else
			Time.fixedDeltaTime = standardFixedTime;

		//tests - overrides online- offline only
		if (rN != null)
		{
			if (rN.airplaneData.tests)
			{
				Time.fixedDeltaTime = standardFixedTime;
				return;
			}
		}


		//if we got here, request data		
		//SendMessage();

		
		if (handshakeTimer - handshakeInterval >= 0 && !waitingOnResponse)
		{
			Debug.Log("Started thread");
		//	Thread thread = new Thread(() => HandShake());
			//thread.IsBackground = true;
			//thread.Start();//does this close automatically?

			handshakeTimer = 0f;
		}
		else
			handshakeTimer += Time.deltaTime;

		
		
			
	}

	IEnumerator Listener()
    {

		while (true)
		{
			byte[] receivedData = listener.Receive(ref listenEndPoint);
			Debug.Log("Decoded data is:");
			Debug.Log(System.Text.Encoding.ASCII.GetString(receivedData)); //should be "Hello World" sent from above client
			yield return new WaitForFixedUpdate();

		}
	}

    /// <summary> 	
    /// Setup socket connection. 	
    /// </summary> 	
    private void ConnectToTcpServer () 
	{ 		
		try {
			

			if (autoScan && !hostFound)
			{
				//Debug.Log("Looking for server");
				//check if anything  on socket

				//look for local connection before going to wifi
				if (!localScanAttempted)
				{
					hostName = "127.0.0.1";
				}
				else
				{
					hostName = "192.168." + ip3.ToString() + "." + ip4.ToString();
				}

				Thread thread = new Thread(() => ListenForData(hostName));
				thread.IsBackground = true;
				thread.Start();

				//let user know we are scanning	
				if(autoScan)
					menuHandler.scanDebug.GetComponent<Text>().text = "Scanning IP: " + hostName.ToString(); ;

				if (localScanAttempted)
				{
					//push to 255 and move ip3 up
					ip4++;
					if (ip4 > 255)
					{
						ip3++;
						ip4 = 0;
					}

					if (ip3 > 255)
					{
					//	Debug.Log("Did not find server");


						//restart -if people have strange ips they probably know about it and can use direct connection option
						ip3 = 0;
						ip4 = 0;

					}
				}

				//first frame we attempt local scan, then on to wifi
				localScanAttempted = true;

			}

			else
            {
				
				//Debug.Log("Starting new thread");

				menuHandler.scanDebug.GetComponent<Text>().text = "Attempting Connection: " + userIP.ToString() +" : " + portNumber ;
				//use value entered by user in hostName
				Thread thread = new Thread(() => ListenForData(hostName));
				thread.IsBackground = true;
				thread.Start();//does this close automatically?
				
			}


		} 		
		catch (Exception e) { 			
			Debug.Log("On client connect exception " + e); 		
		} 	
	}  	
	/// <summary> 	
	/// Runs in background clientReceiveThread; Listens for incomming data. 	
	/// </summary>     
	private void ListenForData(string hostName) { 		
		try {
			
			socketConnection = new TcpClient(hostName, portNumber);
			//array to read stream in to
			Byte[] bytes = new Byte[1024];             
					
			using (NetworkStream stream = socketConnection.GetStream()) 
			{
				//is we have a network stream, save this hostname
				hostFound = true;

				//unpack received stream
				//program version float
				//planetype string size
				//planetype string data size
				//float array containing instrument/dial values

				int length; 					
				// Read incoming stream into byte arrary. 					
				while ((length = stream.Read(bytes, 0, bytes.Length)) != 0) 
				{
					Debug.Log("received");

					int p = 0;

					//set length sent from server	
					int floatArrayLength = 14;
					int floatArrayLengthBytes = 4 * floatArrayLength; //4 bytes for float * array length
					//float array
					float[] floats = GetFloats(bytes, p, floatArrayLength);

					if (!testPrediction)
					{
						//set Il2 game data for client
						iL2GameDataClient.altitude = floats[0];
						iL2GameDataClient.mmhg = floats[1];
						iL2GameDataClient.airspeed = floats[2];
						//save previous heading before asigning new heading - needed for turn co-ordinator needle
						iL2GameDataClient.headingPreviousPrevious = iL2GameDataClient.headingPrevious;
						iL2GameDataClient.headingPrevious = iL2GameDataClient.heading;
						iL2GameDataClient.heading = floats[3];
						iL2GameDataClient.pitch = floats[4];
						iL2GameDataClient.rollPrev = iL2GameDataClient.roll;
						iL2GameDataClient.roll = floats[5];
						iL2GameDataClient.verticalSpeed = floats[6];
						iL2GameDataClient.turnCoordinatorBall = floats[7];
						iL2GameDataClient.turnCoordinatorNeedle = floats[8];
						iL2GameDataClient.rpms[0] = floats[9];
						iL2GameDataClient.rpms[1] = floats[10];
						iL2GameDataClient.rpms[2] = floats[12];
						iL2GameDataClient.rpms[3] = floats[13]; //support for 4 engines (you never know!)
					}
					p += floatArrayLengthBytes;

					//Debug.Log("Reading received data");
					//version number
					//receiving server version from stream (server -> client)
					iL2GameDataClient.serverVersion = BitConverter.ToSingle(bytes,p);
					


					//Debug.Log("Version Number = " + versionNumber);
					p += sizeof(float);

					//plane type string size
					uint stringSize = BitConverter.ToUInt32(bytes,p);
					p += sizeof(uint);
					//plane type string
					string planeType = System.Text.Encoding.UTF8.GetString(bytes, p, (int)stringSize);
					iL2GameDataClient.planeType = planeType;
					p += 64;//chosen max string size (by me)
					
					

					//stopping glitching and setting rest position off centre - move this
					//if (iL2GameDataClient.airspeed < 50 || float.IsNaN( iL2GameDataClient.airspeed )) //do we even want this? why not have needle at 0?
					//iL2GameDataClient.airspeed = 50;
					if (float.IsNaN(iL2GameDataClient.airspeed))
						iL2GameDataClient.airspeed = 0f;

					//Debug.Log("altitude = " + floats[0]);
					//Debug.Log("mmhg = " + floats[1]);
					//Debug.Log("airspeed = " + floats[2]);

						//save rotation of needles				
					if (!testPrediction)
						tcpReceived = true; 

					//keep a track of last receieved time
					timerOfLastReceived = Time.time;

					connected = true;
					connectionTimer = 0f;

				//	Debug.Log("Stream Length = " + length);
				}

				
				
			}
		}         
		catch (Exception ex) {


			if (connectionTimer >= socketTimeoutTime)
			{
				

				connected = false;


			}
			//no stream, server might not be sending anything
			//hostFound = false;
			//socketConnection = null;
			//clientReceiveThread.Abort();
			//Debug.Log("couldn't read");
			//Debug.Log("Socket exception: " + ex);
			//ConnectToTcpServer();
		}     
	}  	
	/// <summary> 	
	/// Send message to server using socket connection. 	
	/// </summary> 	
	public void SendMessage() {
		//Debug.Log("send client");
		if (socketConnection == null || !socketConnection.Connected) 
		{
			connectionTimer += Time.fixedDeltaTime;

			if (autoScan)
			{
				//this creates a small timer for autoscanning
				float thisTimer = 0f; //.1f;
				if (hostFound)
					//if we already found the host and are looking to reconnect, we need to be careful and use the timeout
					thisTimer = socketTimeoutTime;

				

				if (timer >= thisTimer)//timer value of timeout socket setting on server
				{
					//Debug.Log("Sending to server - autoscan");
					timer = 0f;
					ConnectToTcpServer();
				}
				else
					timer += Time.fixedDeltaTime;

				return;
			}
			else
			{
				//use timer so we don't overload server socket - if not connected
				float thisTimer = 0;
				if (!hostFound)
					thisTimer = socketTimeoutTime;

				if (timer >= thisTimer)//timer value of timeout socket setting on server
				{
					//Debug.Log("Sending to server");
					timer = 0f;
					ConnectToTcpServer();
				}
				else
					timer += Time.fixedDeltaTime;

				return;
			}
		}		  		
		try 
		{
			if(Time.time - timerOfLastReceived > socketTimeoutTime)
            {
				Debug.Log("last received time out, resetting");
				//stop sending requests to server, and start to look for another server socket. server may have reset
				socketConnection = null;
				
				if(autoScan)
                {
					//restart? or keep last found host
					//ip3 = 0;
					//ip4 =0;
					hostFound = false;
				}
				else
                {
					//set this so reconnecting to server doesn't spam the socket
					hostFound = false;
				}

				
				return;
            }

			// Get a stream object for writing. 			
			NetworkStream stream = socketConnection.GetStream(); 			
			if (stream.CanWrite) {
				//string clientMessage = "This is a message from one of your clients."; 				
				// Convert string message to byte array.   
				
				//evcode 0x01 - instrument data
				byte[] clientMessageAsByteArray = new byte[1]{ 0x01 };// Encoding.ASCII.GetBytes(clientMessage); 				
				// Write byte array to socketConnection stream.                 
				stream.Write(clientMessageAsByteArray, 0, clientMessageAsByteArray.Length);                 
				//Debug.Log("Client sent his message - should be received by server");

				//timerOfLastReceived = Time.time;

				waitingOnResponse = true;
			}         
		} 		
		catch (Exception ex) 
		{
			//socketConnection = null;
			Debug.Log("Need to Reconnect");
			Debug.Log("Socket exception: " + ex);         
		}     
	}

	static float[] GetFloats(byte[] bytes, int offset, int floatArrayLength)
	{
		try
		{
			var result = new float[floatArrayLength];
			Buffer.BlockCopy(bytes, offset, result, 0, floatArrayLength *4); //* 4 for 4byte floats

			return result;

		}
		catch(Exception ex)
        {
			return null;
        }
	
	}

	////new 
	///

	void HandShake()
    {
		Debug.Log("Handshake");
		//ping host name and look for a response
		try
		{
			Debug.Log("try");
			socketConnection = new TcpClient(hostName, portNumber);
			//array to read stream in to
			Byte[] bytes = new Byte[1024];

			NetworkStream stream = socketConnection.GetStream();
			if (stream.CanWrite)
			{
				//string clientMessage = "This is a message from one of your clients."; 				
				// Convert string message to byte array.   

				//evcode 0x01 - instrument data
				byte[] clientMessageAsByteArray = new byte[1] { 0x01 };// Encoding.ASCII.GetBytes(clientMessage); 				
																	   // Write byte array to socketConnection stream.                 
				stream.Write(clientMessageAsByteArray, 0, clientMessageAsByteArray.Length);
				//Debug.Log("Client sent his message - should be received by server");

				//timerOfLastReceived = Time.time;

				waitingOnResponse = true;

				
			}
		}
		catch
		{
			Debug.Log("caught ex");
		}
	}

	void UDPSender()
    {
		byte[] data = System.Text.Encoding.ASCII.GetBytes("Hello World");
		string ipAddress = "127.0.0.1";
		int sendPort = 11200;

		while (true)
		{
			try
			{
				using (var client = new UdpClient())
				{
					IPEndPoint ep = new IPEndPoint(IPAddress.Parse(ipAddress), sendPort);
					client.Connect(ep);
					client.Send(data, data.Length);
				}
			}
			catch (Exception ex)
			{
				Debug.Log(ex.ToString());
			}
		}
	}

	void UDPListener()
    {
		bool done = false;
		int listenPort = 11200;
		using (UdpClient listener = new UdpClient(listenPort))
		{
			IPEndPoint listenEndPoint = new IPEndPoint(IPAddress.Any, listenPort);
			while (enabled)
			{
				byte[] receivedData = listener.Receive(ref listenEndPoint);
				Debug.Log("Decoded data is:");
				Debug.Log(System.Text.Encoding.ASCII.GetString(receivedData)); //should be "Hello World" sent from above client
			}
		}


	}


}