using System;
using System.Linq;
using System.Collections.Generic;
using Embeddings.Embedding;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using System.Data.SqlClient;

#if NETFRAMEWORK
using System.Data.Entity;
using System.Data.Entity.Infrastructure;
#else
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
#endif

namespace Embeddings
{
	/// <summary>Embeddings Context</summary>
	public partial class EmbeddingsContext
	{

		/// <summary>
		/// Get similar file parts.
		/// </summary>
		/// <param name="vectors">Embedding vectors.</param>
		/// <param name="skip">Skip records.</param>
		/// <param name="take">Take records.</param>
		/// <returns>Similar file parts with the most similar on top.</returns>
		public async Task<List<FilePart>> sp_getSimilarFileParts(
			string groupName, long groupFlag,
			float[] vectors, int skip, int take,
			CancellationToken cancellationToken = default
			)
		{
			await Task.Delay(0);
			// Convert embedding to the format expected by SQL Server.
			var vectorsValue = VectorToBinary(vectors);
			var parameters = new SqlParameter[]
			{
				new SqlParameter("@groupName", groupName),
				new SqlParameter("@groupFlag", groupFlag),
				new SqlParameter("@vectors", vectorsValue),
				new SqlParameter("@skip", skip),
				new SqlParameter("@take", take),
			};
			var sqlCommand = "EXEC [Embedding].[sp_getSimilarFileParts] @groupName, @groupFlag, @vectors, @skip, @take";
#if NETFRAMEWORK
			var items = FileParts.SqlQuery(sqlCommand, parameters).ToList();
#else
			var items = FileParts.FromSqlRaw(sqlCommand, parameters).ToList();
#endif
			return items;
		}

		public DbConnection GetConnection()
		{
#if NETFRAMEWORK
			return Database.Connection;
#else
			return Database.GetDbConnection();
#endif
		}


		private static DbParameter AddParameter(DbCommand command, string parameterName, object value)
		{
			if (value is null)
				return null;
			var parameter = command.CreateParameter();
			parameter.ParameterName = parameterName;
			parameter.Value = value;
			command.Parameters.Add(parameter);
			return parameter;
		}


		/// Convert embedding vectors to byte array.
		/// </summary>
		/// <param name="vectors">Embedding vectors.</param>
		/// <returns>Byte array.</returns>
		static byte[] VectorToBinary(float[] vectors)
		{
			byte[] bytes = new byte[vectors.Length * sizeof(float)];
			Buffer.BlockCopy(vectors, 0, bytes, 0, bytes.Length);
			return bytes;
		}

	}
}
