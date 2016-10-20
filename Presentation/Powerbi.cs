/*!
* DisplayMonkey source file
* http://displaymonkey.org
*
* Copyright (c) 2015 Fuel9 LLC and contributors
*
* Released under the MIT license:
* http://opensource.org/licenses/MIT
*/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Web.Script.Serialization;
using DisplayMonkey.AzureUtil;
using System.Threading.Tasks;

namespace DisplayMonkey
{
    public class Powerbi : Frame
	{
        public string TargetUrl { get; private set; }

        public string Action { get; private set; }

        public async Task<string> GetAccessTokenAsync()
        {
            string accessToken = null;

            await DataAccess.ExecuteTransactionAsync(async (cnn, trn) => {
                using (SqlCommand cmd = new SqlCommand()
                {
                    CommandType = CommandType.Text,
                    CommandText = "select top 1 * from AzureAccount with(updlock,rowlock) where AccountId=@accountId",
                    Connection = cnn,
                    Transaction = trn,
                })
                using (SqlDataAdapter da = new SqlDataAdapter(cmd))
                using (DataSet ds = new DataSet())
                {
                    cmd.Parameters.AddWithValue("@accountId", this.AccountId);
                    da.Fill(ds);
                    if (ds.Tables[0].Rows.Count > 0)
                    {
                        DataRow dr = ds.Tables[0].Rows[0];
                            
                        DateTime? expiresOn = dr.Field<DateTime?>("ExpiresOn");
                        accessToken = dr.StringOrBlank("AccessToken").Trim();

                        if (string.IsNullOrWhiteSpace(accessToken) || !expiresOn.HasValue || expiresOn.Value < DateTime.UtcNow)
                        {
                            TokenInfo token = await Token.GetGrantTypePasswordAsync(
                                (Models.AzureResources)dr.IntOrZero("Resource"),
                                dr.StringOrBlank("ClientId"), 
                                dr.StringOrBlank("ClientSecret"),
                                dr.StringOrBlank("User"),
                                RsaUtil.Decrypt(dr.Field<byte[]>("Password")),
                                dr.Field<string>("TenantId")
                                );
                            using (SqlCommand cmdu = new SqlCommand()
                            {
                                CommandType = CommandType.Text,
                                CommandText = "update AzureAccount set AccessToken=@accessToken, ExpiresOn=@expiresOn where AccountId=@accountId",
                                Connection = cnn,
                                Transaction = trn,
                            })
                            {
                                cmdu.Parameters.AddWithValue("@accountId", this.AccountId);
                                cmdu.Parameters.AddWithValue("@accessToken", token.AccessToken);
                                cmdu.Parameters.AddWithValue("@expiresOn", token.ExpiresOn.AddMinutes(-1)); // allow 1 minute to avoid issues
                                cmdu.ExecuteNonQuery();
                            }
                            accessToken = token.AccessToken;
                        }
                    }                        
                }
            });

            return accessToken;
        }
        
        [ScriptIgnore]
        private int AccountId { get; set; }

        public Powerbi(int frameId)
            : base(frameId)
        {
            _init();
        }

        public Powerbi(Frame frame)
            : base(frame)
		{
            _init();
        }

        private void _init()
        {
            string sql = string.Format("SELECT TOP 1 * FROM Powerbi WHERE FrameId={0}", FrameId);

            using (DataSet ds = DataAccess.RunSql(sql))
            {
                if (ds.Tables[0].Rows.Count > 0)
                {
                    DataRow dr = ds.Tables[0].Rows[0];
                    this.TargetUrl = dr.StringOrBlank("Url").Trim();
                    this.Action = (Models.PowerbiTypes)dr.IntOrZero("Type") == Models.PowerbiTypes.PowerbiType_Report ? "loadReport" : "loadTile";
                    this.AccountId = dr.IntOrZero("AccountId");
                }
            }
        }
    }
}