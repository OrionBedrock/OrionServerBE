using Orion;

var server = new Server();
server.Start();
Console.WriteLine($"{server.Name} host started (skeleton).");
server.Stop();
