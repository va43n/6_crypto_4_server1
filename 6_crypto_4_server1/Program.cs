using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.IO;
using System.Runtime.InteropServices.Marshalling;


namespace _6_crypto_4_server1
{
	internal class Program
	{
		static void Main(string[] args)
		{
			Server server = new Server();

			server.StartServer();
		}
	}

	public class Server
	{
		private TcpListener server;
		private IPAddress host;
		private int port;

		private string allClientsPath = "J:\\С#\\source\\repos\\6_crypto_4_server1\\allClients.txt";
		private string allProductsPath = "J:\\С#\\source\\repos\\6_crypto_4_server1\\allProducts.txt";

		List<List<string>> allClients;
		List<string[]> allProducts;

		List<string[]> onlineClients;

		private string administratorCode = "brigada3";


		public Server()
		{
			server = null;
			host = IPAddress.Parse("0.0.0.0");
			port = 8080;

			onlineClients = new List<string[]>();

			ReadFilesAndSplitData();
		}

		private async void ReadFilesAndSplitData()
		{
			string[] temp;

			string allClientsRaw = await File.ReadAllTextAsync(allClientsPath);
			string allProductsRaw = await File.ReadAllTextAsync(allProductsPath);

			//allClients
			temp = allClientsRaw.Split("\n");
			allClients = new List<List<string>>();

			for (int i = 0; i < temp.Length; i++)
				allClients.Add(temp[i].Split('-').ToList());

			allClients.RemoveAll(x => x.Count == 1);

			//allProducts
			temp = allProductsRaw.Split("\n");
			allProducts = new List<string[]>();

			for (int i = 0; i < temp.Length; i++)
				allProducts.Add(temp[i].Split('-'));

			allProducts.RemoveAll(x => x.Length == 1);
		}

		public void StartServer()
		{
			Thread acceptClientThread, waitForStopMessageThread;

			try
			{
				server = new TcpListener(host, port);
				server.Start();
				Console.WriteLine($"START SERVER\n\thost: {host}\n\tport: {port}");

				acceptClientThread = new Thread(() => AcceptClients());
				acceptClientThread.Start();

				waitForStopMessageThread = new Thread(() => WaitForStopMessage());
				waitForStopMessageThread.Start();
				waitForStopMessageThread.Join();

				SaveDataInFiles();

				acceptClientThread.Abort();
			}
			catch (Exception exc)
			{
				Console.WriteLine(exc.Message);
			}
			finally
			{
				server.Stop();
			}
		}

		private void AcceptClients()
		{
			TcpClient client;
			Thread newClientThread;

			try
			{
				while (true)
				{
					client = server.AcceptTcpClient();
					Console.WriteLine($"ACCEPT CLIENT\n\tfrom: {client.Client.LocalEndPoint}");

					newClientThread = new Thread(() => ReceiveDataFromClient(client));
					newClientThread.Start();
				}
			}
			catch
			{
				Console.WriteLine("ACCEPT CLIENTS THREAD ABORT");
			}
		}

		private void WaitForStopMessage()
		{
			string message;

			while (true)
			{
				message = Console.ReadLine();
				if (message == "STOP")
					break;
			}
		}

		private async void SaveDataInFiles()
		{
			string text = "";
			string temp;

			//allClients
			for (int i = 0; i < allClients.Count; i++)
			{
				temp = string.Join("-", allClients[i]);
				text += temp + "\n";
			}

			await File.WriteAllTextAsync(allClientsPath, text);

			text = "";
			//allProducts
			for (int i = 0; i < allProducts.Count; i++)
			{
				temp = string.Join("-", allProducts[i]);
				text += temp + "\n";
			}

			await File.WriteAllTextAsync(allProductsPath, text);
		}

		private void ReceiveDataFromClient(TcpClient client)
		{
			NetworkStream stream = client.GetStream();
			byte[] receivedMessageBytes = new byte[256], responseMessageBytes;
			int bytesRead = 0;
			string receivedMessage, responseMessage;
			string[] splitReceivedMessage = null;
			string name = "", password = "";

			while (true)
			{
				try
				{
					bytesRead = stream.Read(receivedMessageBytes, 0, receivedMessageBytes.Length);

					if (bytesRead == receivedMessageBytes.Length)
						break;

					receivedMessage = Encoding.UTF8.GetString(receivedMessageBytes, 0, bytesRead);
					Console.WriteLine($"RECEIVE MESSAGE\n\tfrom: {client.Client.LocalEndPoint}\n\tmessage: {receivedMessage}");

					splitReceivedMessage = receivedMessage.Split('-');
					if (splitReceivedMessage[0] == "sin" && name == "" && password == "")
					{
						name = splitReceivedMessage[1];
						password = splitReceivedMessage[2];
					}

					if (splitReceivedMessage[0] == "out")
						break;

					//Get All Products || Buy PRoduct || Get Your Products || Get Online Clients
					else if (splitReceivedMessage[0] == "gap" || splitReceivedMessage[0] == "bpr" || splitReceivedMessage[0] == "gyp" || splitReceivedMessage[0] == "goc")
					{
						CheckHardClientMessage(splitReceivedMessage, stream);
						Console.WriteLine($"SENT RESPONSE\n\tto: {client.Client.LocalEndPoint}\n\tmessage: {"big message"}");
					}

					else
					{
						responseMessage = CheckSimpleClientMessage(splitReceivedMessage);

						responseMessageBytes = Encoding.UTF8.GetBytes(responseMessage, 0, responseMessage.Length);
						stream.Write(responseMessageBytes, 0, responseMessageBytes.Length);
						Console.WriteLine($"SENT RESPONSE\n\tto: {client.Client.LocalEndPoint}\n\tmessage: {responseMessage}");
					}
				}
				catch
				{
					break;
				}
				Thread.Sleep(500);
			}

			for (int i = 0; i < onlineClients.Count; i++)
				if (onlineClients[i][0] == name && onlineClients[i][1] == password)
				{
					onlineClients.RemoveAt(i);
					break;
				}

			Console.WriteLine($"USER DISCONNECTED\n\tip: {client.Client.LocalEndPoint}");
			client.Close();
		}

		private string CheckSimpleClientMessage(string[] splitReceivedMessage)
		{
			if (splitReceivedMessage.Length < 1)
			{
				//NO Data
				return "nod";
			}

			//Sign UP
			//0 - sup, 1 - name, 2 - password, 3 - repeat password,
			//4 - role, 5 - code
			if (splitReceivedMessage[0] == "sup")
			{
				if (splitReceivedMessage.Length != 5 && splitReceivedMessage.Length != 6)
					// Not Enough Arguments
					return "nea";

				if (splitReceivedMessage[1].Length > 20 || splitReceivedMessage[1].Length < 3)
					//bad Length of NaMe
					return "lnm";

				foreach (char ch in splitReceivedMessage[1])
					if (Constants.nameAlphabet.IndexOf(ch) == -1)
						//Bad NaMe
						return "bnm";

				for (int i = 0; i < allClients.Count; i++)
				{
					if (splitReceivedMessage[1] == allClients[i][0])
						//name EXiSt
						return "exs";
				}

				if (splitReceivedMessage[2].Length > 20 || splitReceivedMessage[2].Length < 3 || splitReceivedMessage[3].Length > 20 || splitReceivedMessage[3].Length < 3)
					//bad Length of PassWord
					return "lpw";

				foreach (char ch in splitReceivedMessage[2])
					if (Constants.passwordAlphabet.IndexOf(ch) == -1)
						//Bad PassWord
						return "bpw";

				foreach (char ch in splitReceivedMessage[3])
					if (Constants.passwordAlphabet.IndexOf(ch) == -1)
						//Bad PassWord
						return "bpw";

				if (splitReceivedMessage[2] != splitReceivedMessage[3])
					//Diffrent PassWords
					return "dpw";

				int role;
				try
				{
					role = Convert.ToInt32(splitReceivedMessage[4]);
				}
				catch
				{
					//WRong Role
					return "wrr";
				}
				if (role < 0 || role > 2)
					//WRong Role
					return "wrr";

				if (role == 2 && splitReceivedMessage.Length == 5)
					//WRong Code
					return "wrc";

				if (role == 2 && splitReceivedMessage[5] != administratorCode)
					//WRong Code
					return "wrc";

				//sign up client
				List<string> newClient;

				if (role == 0)
					splitReceivedMessage[4] = "buy";
				else if (role == 1)
					splitReceivedMessage[4] = "sel";
				else if (role == 2)
					splitReceivedMessage[4] = "adm";

				newClient = [splitReceivedMessage[1], splitReceivedMessage[2], splitReceivedMessage[4]];
				if (role == 0)
					newClient.Add("0");

				allClients.Add(newClient);

				return "yes";
			}

			//Sign IN
			//0 - sin, 1 - name, 2 - password
			else if (splitReceivedMessage[0] == "sin")
			{
				if (splitReceivedMessage.Length != 3)
					// Not Enough Arguments
					return "nea";

				if (splitReceivedMessage[1].Length > 20 || splitReceivedMessage[1].Length < 3)
					//bad Length of NaMe
					return "lnm";

				foreach (char ch in splitReceivedMessage[1])
					if (Constants.nameAlphabet.IndexOf(ch) == -1)
						//Bad NaMe
						return "bnm";

				bool isExist = false;
				int clientPosition = 0;
				for (int i = 0; i < allClients.Count; i++)
				{
					if (splitReceivedMessage[1] == allClients[i][0])
					{
						isExist = true;
						clientPosition = i;
						break;
					}
				}
				if (!isExist)
					//UNknown Name
					return "unn";

				if (splitReceivedMessage[2].Length > 20 || splitReceivedMessage[2].Length < 3)
					//bad Length of PassWord
					return "lpw";

				foreach (char ch in splitReceivedMessage[2])
					if (Constants.passwordAlphabet.IndexOf(ch) == -1)
						//Bad PassWord
						return "bpw";

				if (splitReceivedMessage[2] != allClients[clientPosition][1])
					//WRong Password
					return "wrp";

				for (int i = 0; i < onlineClients.Count; i++)
					if (onlineClients[i][0] == splitReceivedMessage[1])
						//Client Already Online
						return "cao";
				onlineClients.Add([splitReceivedMessage[1], splitReceivedMessage[2]]);

				//buy or sel or adm
				if (allClients[clientPosition][2] == "buy")
					return $"{allClients[clientPosition][2]}-{allClients[clientPosition][3]}";
				return allClients[clientPosition][2];
			}

			//Get BaLance
			//0 - gbl, 1 - name, 2 - password
			else if (splitReceivedMessage[0] == "gbl")
			{
				if (splitReceivedMessage.Length != 3)
					// Not Enough Arguments
					return "nea";

				for (int i = 0; i < allClients.Count; i++)
				{
					if (splitReceivedMessage[1] == allClients[i][0] && splitReceivedMessage[2] == allClients[i][1] && (allClients[i][2] == "buy" || allClients[i][2] == "adm"))
					{
						if (allClients[i][2] == "adm")
							return "-1";

						return allClients[i][3];
					}
				}
			}

			//Add Money to Balance
			//0 - amb, 1 - name, 2 - password
			else if (splitReceivedMessage[0] == "amb")
			{
				decimal currentBalance;

				if (splitReceivedMessage.Length != 3)
					// Not Enough Arguments
					return "nea";

				for (int i = 0; i < allClients.Count; i++)
				{
					if (splitReceivedMessage[1] == allClients[i][0] && splitReceivedMessage[2] == allClients[i][1] && (allClients[i][2] == "buy" || allClients[i][2] == "adm"))
					{
						if (allClients[i][2] == "adm")
						{
							//ADMin
							return "adm";
						}

						currentBalance = Convert.ToDecimal(allClients[i][3]);

						if (Constants.maxBalance - currentBalance < 1000)
							//Too Much Money
							return "tmm";

						currentBalance += 1000M;
						allClients[i][3] = currentBalance.ToString();

						return allClients[i][3];
					}
				}
			}

			//Remove PRoduct
			//0 - rpr, 1 - name, 2 - password, 3 - product name
			//4 - cost, 5 - seller
			else if (splitReceivedMessage[0] == "rpr")
			{
				if (splitReceivedMessage.Length != 6)
					//Not Enough Arguments
					return "nea";

				int productPosition = 0;
				bool isExist = false;

				for (int i = 0; i < allClients.Count; i++)
				{
					if (allClients[i][0] == splitReceivedMessage[1] && allClients[i][1] == splitReceivedMessage[2] && allClients[i][2] == "sel")
					{
						isExist = true;
						break;
					}
				}
				if (!isExist)
					//UNknown Name
					return "unn";

				isExist = false;
				for (int i = 0; i < allProducts.Count; i++)
				{
					if (allProducts[i][0] == splitReceivedMessage[5] && allProducts[i][1] == splitReceivedMessage[3] && allProducts[i][2] == splitReceivedMessage[4])
					{
						productPosition = i;
						isExist = true;
						break;
					}
				}
				if (!isExist)
					//UNknown Name
					return "unn";

				allProducts.RemoveAt(productPosition);

				return "yes";
			}

			//Add PRoduct
			//0 - apr, 1 - name, 2 - password, 3 - prod. name, 4 - cost
			else if (splitReceivedMessage[0] == "apr")
			{
				decimal cost;

				if (splitReceivedMessage.Length != 5)
					//Not Enough Arguments
					return "nea";

				bool isExist = false;

				for (int i = 0; i < allClients.Count; i++)
				{
					if (allClients[i][0] == splitReceivedMessage[1] && allClients[i][1] == splitReceivedMessage[2] && allClients[i][2] == "sel")
					{
						isExist = true;
						break;
					}
				}
				if (!isExist)
					//UNknown Name
					return "unn";

				if (allProducts.Count >= Constants.maxNumberOfProducts)
					//Too Many Products
					return "tmp";

				foreach (char ch in splitReceivedMessage[3])
					if (Constants.productAlphabet.IndexOf(ch) == -1)
						//Bad Product Name
						return "bpn";

				if (splitReceivedMessage[3].Length > 50 || splitReceivedMessage[3].Length < 3)
					//bad Length of PRoduct
					return "lpr";

				if (splitReceivedMessage[4].Length < 1 || splitReceivedMessage[4].Length > 50)
					//Bad Length of Number
					return "bln";

				try
				{
					cost = Convert.ToDecimal(splitReceivedMessage[4]);
				}
				catch
				{
					//Not A Number
					return "nan";
				}

				if (cost < 1M || cost > 999999999999M)
					//Bad NUmber
					return "bnu";

				allProducts.Add([splitReceivedMessage[1], splitReceivedMessage[3], splitReceivedMessage[4]]);

				return "yes";
			}

			//UNknown Message
			return "unm";
		}

		private void CheckHardClientMessage(string[] splitReceivedMessage, NetworkStream stream)
		{
			string message, response;
			byte[] messageBytes, responseBytes;
			int bytesRead;

			// Get All Products
			//0 - gap, 1 - name, 2 - password
			if (splitReceivedMessage[0] == "gap")
			{
				bool isExist = false;

				for (int i = 0; i < allClients.Count; i++)
				{
					if (allClients[i][0] == splitReceivedMessage[1] && allClients[i][1] == splitReceivedMessage[2] && (allClients[i][2] == "buy" || allClients[i][2] == "adm"))
					{
						isExist = true;
						break;
					}
				}
				if (!isExist)
				{
					//UNknown Name
					message = "unn";
					messageBytes = Encoding.UTF8.GetBytes(message, 0, message.Length);
					stream.Write(messageBytes, 0, messageBytes.Length);
					return;
				}
				
				message = "yes";
				messageBytes = Encoding.UTF8.GetBytes(message, 0, message.Length);
				stream.Write(messageBytes, 0, messageBytes.Length);

				responseBytes = new byte[3];
				bytesRead = stream.Read(responseBytes, 0, responseBytes.Length);
				response = Encoding.UTF8.GetString(responseBytes, 0, bytesRead);
				if (response != "nxt")
				{
					Console.WriteLine("CANT SEND ALL");
					return;
				}

				message = allProducts.Count.ToString();
				messageBytes = Encoding.UTF8.GetBytes(message, 0, message.Length);
				stream.Write(messageBytes, 0, messageBytes.Length);

				for (int i = 0; i < allProducts.Count; i++)
				{
					bytesRead = stream.Read(responseBytes, 0, responseBytes.Length);
					response = Encoding.UTF8.GetString(responseBytes, 0, bytesRead);
					
					//NeXT
					if (response != "nxt")
					{
						Console.WriteLine("CANT SEND ALL");
						return;
					}

					message = $"{allProducts[i][0]}-{allProducts[i][1]}-{allProducts[i][2]}";
					messageBytes = Encoding.UTF8.GetBytes(message, 0, message.Length);
					stream.Write(messageBytes, 0, messageBytes.Length);
				}
			}

			//Buy PRoduct
			//0 - bpr, 1 - name, 2 - password, 3 - product name
			//4 - cost, 5 - seller
			else if (splitReceivedMessage[0] == "bpr")
			{
				int clientPosition = 0, productPosition = 0;
				decimal balance = 0M, cost = 0M;
				bool isExist = false;

				for (int i = 0; i < allClients.Count; i++)
				{
					if (allClients[i][0] == splitReceivedMessage[1] && allClients[i][1] == splitReceivedMessage[2] && (allClients[i][2] == "buy" || allClients[i][2] == "adm"))
					{
						clientPosition = i;
						balance = allClients[i][2] == "adm" ? -1 : Convert.ToDecimal(allClients[i][3]);
						isExist = true;
						break;
					}
				}
				if (!isExist)
				{
					//UNknown Name
					message = "unn";
					messageBytes = Encoding.UTF8.GetBytes(message, 0, message.Length);
					stream.Write(messageBytes, 0, messageBytes.Length);
					return;
				}

				isExist = false;
				for (int i = 0; i < allProducts.Count; i++)
				{
					if (allProducts[i][0] == splitReceivedMessage[5] && allProducts[i][1] == splitReceivedMessage[3] && allProducts[i][2] == splitReceivedMessage[4])
					{
						cost = Convert.ToDecimal(splitReceivedMessage[4]);
						productPosition = i;
						isExist = true;
						break;
					}
				}
				if (!isExist)
				{
					//UNknown Name
					message = "unn";
					messageBytes = Encoding.UTF8.GetBytes(message, 0, message.Length);
					stream.Write(messageBytes, 0, messageBytes.Length);
					return;
				}

				message = "yes";
				messageBytes = Encoding.UTF8.GetBytes(message, 0, message.Length);
				stream.Write(messageBytes, 0, messageBytes.Length);

				responseBytes = new byte[3];
				bytesRead = stream.Read(responseBytes, 0, responseBytes.Length);
				response = Encoding.UTF8.GetString(responseBytes, 0, bytesRead);
				//NeXT
				if (response != "nxt")
				{
					Console.WriteLine("CANT BUY PRODUCT");
					return;
				}

				if (balance == -1M)
				{
					allProducts.RemoveAt(productPosition);
					message = "yes";
				}
				else if (balance >= cost)
				{
					balance -= cost;
					allClients[clientPosition][3] = balance.ToString();
					allProducts.RemoveAt(productPosition);
					message = "yes";
				}
				else
				{
					//Not Enough Money
					message = "nem";
				}

				messageBytes = Encoding.UTF8.GetBytes(message, 0, message.Length);
				stream.Write(messageBytes, 0, messageBytes.Length);
			}

			//Get Your Products
			//0 - gyp, 1 - name, 2 - password
			else if (splitReceivedMessage[0] == "gyp")
			{
				long numberOfYourProducts = 0;
				bool isExist = false;

				for (int i = 0; i < allClients.Count; i++)
				{
					if (allClients[i][0] == splitReceivedMessage[1] && allClients[i][1] == splitReceivedMessage[2] && allClients[i][2] == "sel")
					{
						isExist = true;
						break;
					}
				}
				if (!isExist)
				{
					//UNknown Name
					message = "unn";
					messageBytes = Encoding.UTF8.GetBytes(message, 0, message.Length);
					stream.Write(messageBytes, 0, messageBytes.Length);
					return;
				}

				message = "yes";
				messageBytes = Encoding.UTF8.GetBytes(message, 0, message.Length);
				stream.Write(messageBytes, 0, messageBytes.Length);
				Console.WriteLine($"SENT RESPONSE\n\tmessage: {message}");

				responseBytes = new byte[3];
				bytesRead = stream.Read(responseBytes, 0, responseBytes.Length);
				response = Encoding.UTF8.GetString(responseBytes, 0, bytesRead);
				Console.WriteLine($"RECEIVE MESSAGE\n\tmessage: {response}");
				if (response != "nxt")
				{
					Console.WriteLine("CANT SEND PRODUCTS");
					return;
				}

				for (int i = 0; i < allProducts.Count; i++)
					if (allProducts[i][0] == splitReceivedMessage[1])
						numberOfYourProducts++;

				message = numberOfYourProducts.ToString();
				messageBytes = Encoding.UTF8.GetBytes(message, 0, message.Length);
				stream.Write(messageBytes, 0, messageBytes.Length);
				Console.WriteLine($"SENT RESPONSE\n\tmessage: {message}");

				for (int i = 0; i < allProducts.Count; i++)
				{
					if (allProducts[i][0] != splitReceivedMessage[1])
						continue;
					bytesRead = stream.Read(responseBytes, 0, responseBytes.Length);
					response = Encoding.UTF8.GetString(responseBytes, 0, bytesRead);
					Console.WriteLine($"RECEIVE MESSAGE\n\tmessage: {response}");

					//NeXT
					if (response != "nxt")
					{
						Console.WriteLine("CANT SEND ALL");
						return;
					}

					message = $"{allProducts[i][0]}-{allProducts[i][1]}-{allProducts[i][2]}";
					messageBytes = Encoding.UTF8.GetBytes(message, 0, message.Length);
					stream.Write(messageBytes, 0, messageBytes.Length);
					Console.WriteLine($"SENT RESPONSE\n\tmessage: {message}");
				}
			}

			//Get Online Users
			//0 - goc, 1 - name, 2 - password
			else if (splitReceivedMessage[0] == "goc")
			{
				long numberOfYourProducts = 0;
				bool isExist = false;

				for (int i = 0; i < allClients.Count; i++)
				{
					if (allClients[i][0] == splitReceivedMessage[1] && allClients[i][1] == splitReceivedMessage[2] && allClients[i][2] == "adm")
					{
						isExist = true;
						break;
					}
				}
				if (!isExist)
				{
					//UNknown Name
					message = "unn";
					messageBytes = Encoding.UTF8.GetBytes(message, 0, message.Length);
					stream.Write(messageBytes, 0, messageBytes.Length);
					return;
				}

				message = "yes";
				messageBytes = Encoding.UTF8.GetBytes(message, 0, message.Length);
				Console.WriteLine($"SENT RESPONSE\n\tmessage: {message}");
				stream.Write(messageBytes, 0, messageBytes.Length);

				responseBytes = new byte[3];
				bytesRead = stream.Read(responseBytes, 0, responseBytes.Length);
				response = Encoding.UTF8.GetString(responseBytes, 0, bytesRead);
				Console.WriteLine($"RECEIVE MESSAGE\n\tmessage: {response}");
				
				if (response != "nxt")
				{
					Console.WriteLine("CANT SEND PRODUCTS");
					return;
				}

				message = onlineClients.Count.ToString();
				messageBytes = Encoding.UTF8.GetBytes(message, 0, message.Length);
				stream.Write(messageBytes, 0, messageBytes.Length);
				Console.WriteLine($"SENT RESPONSE\n\tmesage: {message}");

				for (int i = 0; i < onlineClients.Count; i++)
				{
					bytesRead = stream.Read(responseBytes, 0, responseBytes.Length);
					response = Encoding.UTF8.GetString(responseBytes, 0, bytesRead);
					Console.WriteLine($"RECEIVE MESSAGE\n\tmessage: {response}");

					//NeXT
					if (response != "nxt")
					{
						Console.WriteLine("CANT SEND ALL CLIENTS");
						return;
					}

					message = $"{onlineClients[i][0]}-{onlineClients[i][1]}";
					messageBytes = Encoding.UTF8.GetBytes(message, 0, message.Length);
					stream.Write(messageBytes, 0, messageBytes.Length);
					Console.WriteLine($"SENT RESPONSE\n\tmesage: {message}");
				}
			}
		}
	}

	public static class Constants
	{
		public static string nameAlphabet = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789_";
		public static string passwordAlphabet = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
		public static string productAlphabet = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZабвгдеёжзийклмнопрстуфхцчшщъыьэюяАБВГДЕЁЖЗИЙКЛМНОПРСТУФХЦЧШЩЪЫЬЭЮЯ0123456789 ";

		public static decimal maxBalance = 999999999999M;
		public static int maxNumberOfProducts = 999999999;
	}
}

