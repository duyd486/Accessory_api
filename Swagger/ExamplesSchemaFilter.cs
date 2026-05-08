using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using Vibra_Dotnet_api.Contracts.Requests;

namespace Vibra_Dotnet_api.Swagger;

public sealed class ExamplesSchemaFilter : ISchemaFilter
{
    public void Apply(OpenApiSchema schema, SchemaFilterContext context)
    {
        if (context.Type == typeof(CreateBillRequest))
        {
            schema.Example = new OpenApiObject
            {
                ["payment_method"] = new OpenApiString("online"),
                ["total_price"] = new OpenApiDouble(199000),
                ["phone"] = new OpenApiString("0900000000"),
                ["address"] = new OpenApiString("123 Nguyen Trai, Q1, HCM"),
                ["items"] = new OpenApiArray
                {
                    new OpenApiObject
                    {
                        ["id"] = new OpenApiLong(1),
                        ["quantity"] = new OpenApiInteger(1),
                        ["total_price"] = new OpenApiDouble(99000),
                        ["price"] = new OpenApiDouble(99000)
                    },
                    new OpenApiObject
                    {
                        ["id"] = new OpenApiLong(2),
                        ["quantity"] = new OpenApiInteger(1),
                        ["total_price"] = new OpenApiDouble(100000),
                        ["price"] = new OpenApiDouble(100000)
                    }
                }
            };
        }

        if (context.Type == typeof(UpdateOrderStatusRequest))
        {
            schema.Example = new OpenApiObject
            {
                ["order_id"] = new OpenApiLong(9),
                ["status"] = new OpenApiInteger(4)
            };
        }

        if (context.Type == typeof(SendFeedbackRequest))
        {
            schema.Example = new OpenApiObject
            {
                ["bill_id"] = new OpenApiLong(9),
                ["score"] = new OpenApiInteger(5),
                ["comment"] = new OpenApiString("S?n ph?m t?t")
            };
        }

        if (context.Type == typeof(UpdateOrCreateProductRequest))
        {
            schema.Example = new OpenApiObject
            {
                ["name"] = new OpenApiString("CD Sample"),
                ["category_id"] = new OpenApiLong(1),
                ["description"] = new OpenApiString("Mô t? s?n ph?m"),
                ["brand"] = new OpenApiString("Sony"),
                ["price"] = new OpenApiDouble(120000),
                ["quantity"] = new OpenApiInteger(10),
                ["total_sold"] = new OpenApiInteger(0),
                ["score"] = new OpenApiDouble(0)
            };
        }

        if (context.Type == typeof(UpdateOrCreateCategoryRequest))
        {
            schema.Example = new OpenApiObject
            {
                ["title"] = new OpenApiString("Rock"),
                ["parent_id"] = new OpenApiLong(0)
            };
        }

        if (context.Type == typeof(UpdateProfileRequest))
        {
            schema.Example = new OpenApiObject
            {
                ["name"] = new OpenApiString("Tom"),
                ["email"] = new OpenApiString("tom@example.com"),
                ["phone"] = new OpenApiString("0900000000"),
                ["address"] = new OpenApiString("HCM"),
                ["current_password"] = new OpenApiString("12345678"),
                ["new_password"] = new OpenApiString("123456789"),
                ["new_password_confirmation"] = new OpenApiString("123456789")
            };
        }
    }
}
