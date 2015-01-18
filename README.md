# Cosmos
C# socket server and client. This application runs by using SocketAsyncEventArgs.

## Server example
```csharp
const int port = 8638;
const int backLog = 100;
const int maxConnections = 1000;
const int receiveBufferSize = 512;
const int sendBufferSize = 512;
const int acceptSocketAsyncEventArgs = 8;

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
	} while (true);

	Trace.WriteLine("Good bye", "[INFO]");
}

static void bootstrap_OnSocketError(object sender, System.Net.Sockets.SocketError socketError)
{
	Console.WriteLine(socketError.ToString());
}

static void bootstrap_OnRead(object sender, Session session)
{
	GreetingMessage greeting = (GreetingMessage)session.Payload;
	Console.WriteLine("Name:{0}, Greeting:{1}", greeting.Name, greeting.Greeting);
	session.Write(greeting);
}

static void bootstrap_OnClosed(object sender, Session session)
{
	Console.WriteLine("Disconnected!!" + session.SessionId);
}

static void bootstrap_OnAccepted(object sender, System.Net.Sockets.Socket socket)
{
	Console.WriteLine("Accepted");
}
```

## Client example
```csharp
const int READ_BUFFER_SIZE = 512;
const int WRITE_BUFFER_SIZE = 512;
const int WRITE_SIMULTANEOUS = 10;

static Bootstrap bootstrap;

static void Main(string[] args)
{
	Setting setting = new Setting(READ_BUFFER_SIZE, WRITE_BUFFER_SIZE, WRITE_SIMULTANEOUS);

	bootstrap = new Bootstrap(setting, new ClassMessageSerializer());

	bootstrap.OnConnected += bootstrap_OnConnected;
	bootstrap.OnDisconnected += bootstrap_OnDisconnected;
	bootstrap.OnRead += bootstrap_OnRead;
	bootstrap.OnSocketError += bootstrap_OnSocketError;

	bootstrap.Connect("127.0.0.1", 8638);

	Console.ReadLine();
}

static void bootstrap_OnSocketError(object sender, System.Net.Sockets.SocketError socketError)
{
	Console.WriteLine("OnSocketError:" + socketError.ToString());
}

static void bootstrap_OnRead(object sender, Session session, object message)
{
	GreetingMessage greeting = (GreetingMessage)message;
	Console.WriteLine("Name:{0}, Greeting:{1}", greeting.Name, greeting.Greeting);
	
	bootstrap.Send(greeting);
}

static void bootstrap_OnDisconnected(object sender)
{
	Console.WriteLine("OnDisconnected");
}

static void bootstrap_OnConnected(object sender)
{
	Console.WriteLine("OnConnected");
	GreetingMessage greeting = new GreetingMessage();
	greeting.Name = Guid.NewGuid().ToString();
	greeting.Greeting = "Hi";

	bootstrap.Send(greeting);
}
```
