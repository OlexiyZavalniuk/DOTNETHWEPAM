using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.DocumentModel;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using Amazon.Runtime.Internal;
using GetAllChats.Models;
using System.Net;
using System.Text.Json;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace GetAllChats;

public class Function
{
    private readonly AmazonDynamoDBClient _client;
    private readonly DynamoDBContext _context;

    public Function()
    {
        _client = new AmazonDynamoDBClient();
        _context = new DynamoDBContext(_client);
    }

    public async Task<APIGatewayProxyResponse> FunctionHandler(APIGatewayProxyRequest request, ILambdaContext context)
    {
        var userId = request.QueryStringParameters["userId"];

		request.QueryStringParameters.TryGetValue("pageSize", out var pageSizeString);
		_ = int.TryParse(pageSizeString, out var pageSize);
		pageSize = pageSize == 0 ? 10 : pageSize;

		request.QueryStringParameters.TryGetValue("pageNumber", out var pageNumberString);
		_ = int.TryParse(pageNumberString, out var pageNumber);
		pageNumber = pageNumber <= 0 ? 1 : pageNumber;

		var startIndex = (pageNumber - 1) * pageSize;

		if (pageSize > 1000 || pageSize < 1)
		{
			return new APIGatewayProxyResponse()
			{
				StatusCode = (int)HttpStatusCode.OK,
				Headers = new Dictionary<string, string>
				{
					{ "Content-Type", "application/json" },
					{ "Access-Control-Allow-Origin", "*" }
				},

				Body = "Invalid pageSize."
			};
		}

		List<Chat> chats = await GetAllChats(userId);
		var result = chats.Skip(startIndex).Take(pageSize).ToList();

		return new APIGatewayProxyResponse
        {
            StatusCode = (int)HttpStatusCode.OK,
            Headers = new Dictionary<string, string>
            {
                { "Content-Type", "application/json" },
                { "Access-Control-Allow-Origin", "*" }
            },

            Body = JsonSerializer.Serialize(result)
        };
    }

    private async Task<List<Chat>> GetAllChats(string userId)
    {
		var user1 = new QueryOperationConfig()
		{
			IndexName = "user1-updatedDt-index",
			KeyExpression = new Expression()
			{
				ExpressionStatement = "user1 = :user",
				ExpressionAttributeValues = new Dictionary<string, DynamoDBEntry>() { { ":user", userId } }
			}
		};
		var user1Results = await _context.FromQueryAsync<Chat>(user1).GetRemainingAsync();

		var user2 = new QueryOperationConfig()
		{
			IndexName = "user2-updatedDt-index",
			KeyExpression = new Expression()
			{
				ExpressionStatement = "user2 = :user",
				ExpressionAttributeValues = new Dictionary<string, DynamoDBEntry>() { { ":user", userId } }
			}
		};
		var user2Results = await _context.FromQueryAsync<Chat>(user2).GetRemainingAsync();

		user1Results.AddRange(user2Results);
		return user1Results.OrderBy(x => x.UpdateDt).ToList();
	}
}
