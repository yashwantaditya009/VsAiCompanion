﻿using Embeddings;
using JocysCom.ClassLibrary;
using JocysCom.VS.AiCompanion.DataClient;
using JocysCom.VS.AiCompanion.Engine.Companions.ChatGPT;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using LiteDB;
using System.Data.Common;
using JocysCom.ClassLibrary.Configuration;
using JocysCom.VS.AiCompanion.DataFunctions;
using Embeddings.Embedding;




#if NETFRAMEWORK
using System.Data.SqlClient;
using System.Data.SQLite;
#else
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
#endif

namespace JocysCom.VS.AiCompanion.Engine
{
	public class EmbeddingHelper
	{
		//public class MyDbConfiguration : DbConfiguration
		//{
		//	public MyDbConfiguration(string connectionString)
		//	{
		//		if (connectionString.Contains(".db"))
		//		{
		//			SetProviderFactory("System.Data.SQLite.EF6", SQLiteProviderFactory.Instance);
		//			SetProviderServices("System.Data.SQLite.EF6", (DbProviderServices)SQLiteProviderFactory.Instance.GetService(typeof(DbProviderServices)));
		//		}
		//		else
		//		{
		//			SetProviderServices("System.Data.SqlClient", SqlProviderServices.inInstance);
		//		}
		//	}
		//}

		public static EmbeddingsContext NewEmbeddingsContext(string connectionStringOrFilPath)
		{
#if NETFRAMEWORK
			//var config = new MyDbConfiguration(connectionString);
			//DbConfiguration.SetConfiguration(config);
			DbConnection connection;
			if (connectionStringOrFilPath.Contains(".db"))
				connection = new SQLiteConnection(connectionStringOrFilPath);
			else
				connection = new SqlConnection(connectionStringOrFilPath);
			var db = new EmbeddingsContext();
			db.Database.Connection.ConnectionString = connectionStringOrFilPath;
#else
			var optionsBuilder = new DbContextOptionsBuilder<EmbeddingsContext>();
			if (connectionStringOrFilPath.EndsWith(".db"))
			{
				var connectionString = SqliteHelper.NewConnection(connectionStringOrFilPath).ConnectionString.ToString();
				optionsBuilder.UseSqlite(connectionString);
			}
			else
				optionsBuilder.UseSqlServer(connectionStringOrFilPath);
			var db = new EmbeddingsContext(optionsBuilder.Options);
#endif
			return db;
		}


		public static async Task ConvertToEmbeddingsCSV(
			string path,
			string connectionString,
			AiService service, string modelName,
			string embeddingGroupName,
			EmbeddingGroup embeddingGroupFlag
			)
		{
			//var service = Global.AppSettings.AiServices.FirstOrDefault(x => x.BaseUrl.Contains("azure"));
			//var url = service.BaseUrl + $"openai/deployments/{model}/embeddings?api-version=2023-05-15";
			var files = Directory.GetFiles(path, "*.txt");
			var db = NewEmbeddingsContext(connectionString);
			var algorithm = System.Security.Cryptography.SHA256.Create();

			for (int i = 0; i < files.Count(); i++)
			{
				var fi = new FileInfo(files[i]);
				var file = db.Files.FirstOrDefault(x => x.Url == fi.FullName);
				var fileHash = JocysCom.ClassLibrary.Security.SHA256Helper.GetHashFromFile(fi.FullName);
				if (file == null)
				{
					file = new Embeddings.Embedding.File();
					db.Files.Add(file);
				}
				//file.GroupName = 
				file.Name = fi.Name;
				file.Url = fi.FullName;
				file.GroupName = embeddingGroupName;
				file.GroupFlag = (long)embeddingGroupFlag;
				file.HashType = "SHA_256";
				file.Hash = fileHash;
				file.Size = fi.Length;
				file.State = (int)ProgressStatus.Completed;
				file.IsEnabled = true;
				file.Created = fi.CreationTime.ToUniversalTime();
				file.Modified = fi.LastWriteTime.ToUniversalTime();
				db.SaveChanges();
				var text = System.IO.File.ReadAllText(fi.FullName);
				var client = new Client(service);
				// Don't split file
				var input = new List<string> { text };
				var results = await client.GetEmbedding(modelName, input);
				var now = DateTime.Now;
				var partHash = algorithm.ComputeHash(System.Text.Encoding.Unicode.GetBytes(text));
				foreach (var result in results)
				{
					var part = new Embeddings.Embedding.FilePart();
					part.Embedding = VectorToBinary(result.Value);
					part.GroupName = embeddingGroupName;
					part.GroupFlag = (long)embeddingGroupFlag;
					part.FileId = file.Id;
					part.EmbeddingModel = modelName;
					part.EmbeddingSize = result.Value.Length;
					part.GroupFlag = (int)embeddingGroupFlag;
					part.Index = 0;
					part.Count = 1;
					part.HashType = "SHA_256";
					part.Hash = partHash;
					part.IsEnabled = true;
					part.Created = now.ToUniversalTime();
					part.Modified = now.ToUniversalTime();
					part.Text = input[0];
					part.TextTokens = Companions.ClientHelper.CountTokens(text);
					db.FileParts.Add(part);
					db.SaveChanges();
				}
			}
		}

		public string Log { get; set; } = "";
		public List<Embeddings.Embedding.File> Files { get; set; }
		public List<Embeddings.Embedding.FilePart> FileParts { get; set; }



		public async Task SearchEmbeddings(EmbeddingsItem item, string message, int skip, int take)
		{
			try
			{
				Log = "Converting message to embedding vectors...";
				if (string.IsNullOrWhiteSpace(item.Message))
				{
					Log += " Message is empty.\r\n";
					return;
				}
				var input = new List<string> { item.Message };
				var client = new Client(item.AiService);
				var results = await client.GetEmbedding(item.AiModel, input);
				Log += " Done.\r\n";
				var expandedTarget = AssemblyInfo.ExpandPath(item.Target);
				var db = NewEmbeddingsContext(expandedTarget);
				var vectors = results[0];
				Log += "Searching on database...";
				if (IsFilePath(item.Target))
				{
					var ids = await GetSimilarFileEmbeddings(expandedTarget, item.EmbeddingGroupName, (long)item.EmbeddingGroupFlag, vectors, item.Take);
					FileParts = db.FileParts
						.Where(x => ids.Contains(x.Id))
						.ToList()
						.OrderBy(x => ids.IndexOf(x.Id))
						.ToList();
				}
				else
				{
					// Convert your embedding to the format expected by SQL Server.
					// This example assumes `results` is the embedding in a suitable binary format.
					var embeddingParam = new SqlParameter("@promptEmbedding", SqlDbType.VarBinary)
					{
						Value = VectorToBinary(vectors)
					};
					var skipParam = new SqlParameter("@skip", SqlDbType.Int) { Value = skip };
					var takeParam = new SqlParameter("@take", SqlDbType.Int) { Value = take };
					// Assuming `FileSimilarity` is the result type.
					var sqlCommand = "EXEC [Embedding].[sp_getSimilarFileEmbeddings] @promptEmbedding, @skip, @take";
#if NETFRAMEWORK
				FileParts = db.Database.SqlQuery<Embeddings.Embedding.FilePart>(
					sqlCommand, embeddingParam, skipParam, takeParam)
					.ToList();
#else
					FileParts = db.FileParts.FromSqlRaw(
						sqlCommand, embeddingParam, skipParam, takeParam)
						.ToList();
#endif

				}
				var fileIds = FileParts.Select(x => x.FileId).Distinct().ToArray();
				Files = db.Files.Where(x => fileIds.Contains(x.Id)).ToList();
				Log += " Done...";
			}
			catch (System.Exception ex)
			{
				Log = ex.ToString();
			}
		}

		public static bool IsFilePath(string stringOrPath)
		{
			var path = AssemblyInfo.ExpandPath(stringOrPath);
			var ext = Path.GetExtension(path).ToLower();
			return ext == ".db";
		}

		public static async Task<List<long>> GetSimilarFileEmbeddings(
			string path,
			string groupName,
			long groupFlag,
			float[] promptVectors, int take)
		{
			var commandText = $@"
                SELECT
					fp.Id,
					fp.FileId,
					fp.Embedding
                FROM FilePart AS fp
                JOIN File AS f ON f.Id = fp.FileId
                WHERE f.GroupName = @GroupName
                AND fp.GroupFlag & @GroupFlag > 0
                AND fp.IsEnabled = 1
                AND f.IsEnabled = 1";
			var connection = SqliteHelper.NewConnection(path);
			var command = SqliteHelper.NewCommand(commandText, connection);
			var nameParam = command.CreateParameter();
			nameParam.ParameterName = "@GroupName";
			nameParam.Value = groupName;
			command.Parameters.Add(nameParam);
			var flagParam = command.CreateParameter();
			flagParam.ParameterName = "@GroupFlag";
			flagParam.Value = (int)groupFlag;
			command.Parameters.Add(flagParam);
			connection.Open();
			var reader = await command.ExecuteReaderAsync();
			var tempResult = new SortedList<float, FilePart>();
			while (await reader.ReadAsync())
			{
				var filePart = ReadFilePartFromReader(reader);
				var partVectors = EmbeddingBase.BinaryToVector(filePart.Embedding);
				var similarity = EmbeddingBase._CosineSimilarity(promptVectors, partVectors);
				// If take list is not filled yet then add and continue.
				if (tempResult.Count < take)
				{
					tempResult.Add(similarity, filePart);
					continue;
				}
				// If similarity less or same then skip and continue.
				if (similarity <= tempResult.Keys[0])
					continue;
				// Replace least similar item with the more similar.
				tempResult.RemoveAt(0);
				tempResult.Add(similarity, filePart);
			}
			var ids = tempResult
				.ToList()
				.OrderByDescending(x => x.Key)
				.Select(x => x.Value.Id)
				.ToList();
			return ids;
		}

		private static FilePart ReadFilePartFromReader(DbDataReader reader)
		{
			var filePart = new FilePart
			{
				Id = reader.GetInt64(reader.GetOrdinal("Id")),
				//GroupName = reader.GetString(reader.GetOrdinal("GroupName")),
				//GroupFlag = reader.GetInt64(reader.GetOrdinal("GroupFlag")),
				FileId = reader.GetInt64(reader.GetOrdinal("FileId")),
				//Index = reader.GetInt32(reader.GetOrdinal("Index")),
				//Count = reader.GetInt32(reader.GetOrdinal("Count")),
				//HashType = reader.GetString(reader.GetOrdinal("HashType")),
				//Hash = (byte[])reader["Hash"],
				//Text = reader.GetString(reader.GetOrdinal("Text")),
				//TextTokens = reader.GetInt64(reader.GetOrdinal("TextTokens")),
				//EmbeddingModel = reader.GetString(reader.GetOrdinal("EmbeddingModel")),
				//EmbeddingSize = reader.GetInt32(reader.GetOrdinal("EmbeddingSize")),
				Embedding = (byte[])reader["Embedding"],
				//IsEnabled = reader.GetBoolean(reader.GetOrdinal("IsEnabled")),
				//Created = reader.GetDateTime(reader.GetOrdinal("Created")),
				//Modified = reader.GetDateTime(reader.GetOrdinal("Modified"))
			};
			return filePart;
		}


		#region Client

		//// Synchronous method to get embeddings
		//public static float[] GetEmbeddings(string url, string apiKey, string text, AiService service)
		//{
		//	if (string.IsNullOrEmpty(url) || string.IsNullOrEmpty(apiKey) || string.IsNullOrEmpty(text))
		//		throw new ArgumentException("URL, API key, and text cannot be null or empty.");
		//	using (var _httpClient = new HttpClient())
		//	{
		//		// Add the necessary headers to the request
		//		_httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
		//		// Create the JSON body
		//		var payload = new payload { text = text };
		//		var json = Client.Serialize(payload);
		//		var httpContent = new StringContent(json, Encoding.UTF8, "application/json");
		//		// Make the POST request
		//		var response = _httpClient.PostAsync(url, httpContent).Result;
		//		response.EnsureSuccessStatusCode();
		//		// Read the response as a string
		//		string responseString = response.Content.ReadAsStringAsync().Result;
		//		// Deserialize the response into a list of embeddings
		//		var result = Client.Deserialize<embedding_response>(responseString);
		//		var embeddings = result.embeddings[0].embedding;
		//		return embeddings;
		//	}
		//}

		/// <summary>
		/// Convert embedding vectors to byte array.
		/// </summary>
		/// <param name="vectors">Embedding vectors.</param>
		/// <returns>Byte array.</returns>
		public static byte[] VectorToBinary(float[] vectors)
		{
			byte[] bytes = new byte[vectors.Length * sizeof(float)];
			Buffer.BlockCopy(vectors, 0, bytes, 0, bytes.Length);
			return bytes;
		}

		private class @payload
		{
			public string text { get; set; }
		}

		// Helper class to deserialize the JSON response
		private class @embedding_response
		{
			public Embedding[] embeddings { get; set; }
		}

		private class Embedding
		{
			public float[] embedding { get; set; }
		}


		#endregion
	}
}
