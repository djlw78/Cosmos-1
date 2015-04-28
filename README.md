# Cosmos
C# socket server and client by using SocketAsyncEventArgs for read and write operation. The purpose of this application is for making game server and client. The client target platform is Unity 3D.

## Server example
```csharp
const int port = 8638;
const int backLog = 100;
const int maxConnections = 1000;
const int receiveBufferSize = 512;
const int sendBufferSize = 512;
const int acceptSocketAsyncEventArgs = 8;

static ClassMessageSerializer messageSerializer = new ClassMessageSerializer();

static void Main(string[] args)
{
	Debug.Listeners.Add(new TextWriterTraceListener(Console.Out));
	Setting setting = new Setting(port, backLog, maxConnections, receiveBufferSize, sendBufferSize, acceptSocketAsyncEventArgs);

	Bootstrap bootstrap = new Bootstrap(setting, new ClassMessageSerializer());

	bootstrap.OnAccepted += bootstrap_OnAccepted;
	bootstrap.OnClosed += bootstrap_OnClosed;
	bootstrap.OnRead += bootstrap_OnRead;
	bootstrap.OnSocketError += bootstrap_OnSocketError;

	bootstrap.Start();

	Trace.WriteLine("Press Ctrl+Z to terminate gracefully the server process...", "[INFO]");
	ConsoleKeyInfo cki;

	do
	{
		cki = Console.ReadKey();

		if ((cki.Modifiers & ConsoleModifiers.Control) != 0)
		{
			if (cki.Key == ConsoleKey.Z)
			{
				bootstrap.Shutdown();
				break;
			}
		}
	}
	while (true);

	Trace.WriteLine("Good bye", "[INFO]");
}

private static void bootstrap_OnSocketError(System.Net.Sockets.SocketError socketError)
{
	Console.WriteLine(socketError.ToString());
}

private static void bootstrap_OnRead(Session session)
{
	GreetingMessage greeting = messageSerializer.Deserialize<GreetingMessage>(session.Payload);
	Console.WriteLine("Name:{0}, Greeting:{1}", greeting.Name, greeting.Greeting);
	session.Write(1, greeting);
}

private static void bootstrap_OnClosed(Session session, System.Net.Sockets.Socket socket)
{
	Console.WriteLine("Disconnected!!" + session.SessionId);
}

private static void bootstrap_OnAccepted(System.Net.Sockets.Socket socket)
{
	Console.WriteLine("Accepted");
}
```

## Client example
```csharp
const int READ_BUFFER_SIZE = 512;
const int WRITE_BUFFER_SIZE = 512;

static Bootstrap bootstrap;
static ClassMessageSerializer messageSerializer = new ClassMessageSerializer();

static void Main(string[] args)
{
	Debug.Listeners.Add(new TextWriterTraceListener(Console.Out));
	Setting setting = new Setting(READ_BUFFER_SIZE, WRITE_BUFFER_SIZE);

	bootstrap = new Bootstrap(setting, new ClassMessageSerializer());

	bootstrap.OnConnected += bootstrap_OnConnected;
	bootstrap.OnDisconnected += bootstrap_OnDisconnected;
	bootstrap.OnRead += bootstrap_OnRead;
	bootstrap.OnSocketError += bootstrap_OnSocketError;

	bootstrap.Connect("127.0.0.1", 8638);

	Console.ReadLine();
}

private static void bootstrap_OnSocketError(System.Net.Sockets.SocketError socketError)
{
	Console.WriteLine("OnSocketError:" + socketError.ToString());
}

private static void bootstrap_OnRead(ushort handlerId, byte[] payload)
{
	GreetingMessage greeting =  messageSerializer.Deserialize<GreetingMessage>(payload);
	Console.WriteLine("Name:{0}, Greeting:{1}", greeting.Name, greeting.Greeting);
	bootstrap.Send<GreetingMessage>(handlerId, greeting);
}

private static void bootstrap_OnDisconnected()
{
	Console.WriteLine("OnDisconnected");
}

private static void bootstrap_OnConnected()
{
	Console.WriteLine("OnConnected");
	GreetingMessage greeting = new GreetingMessage();
	greeting.Name = Guid.NewGuid().ToString();
	greeting.Greeting = "Hi";
	bootstrap.Send<GreetingMessage>(1, greeting);
}
```
