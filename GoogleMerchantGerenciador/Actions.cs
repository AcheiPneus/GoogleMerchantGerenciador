using ControllerDBApi;
using Google.Shopping.Merchant.Products.V1;
using GoogleMerchantApi;
using Grpc.Core;
using NLog;

namespace GoogleMerchantGerenciador;

public class Actions(ControllerDB db)
{
    private readonly MerchantClient _client = new();
    private readonly Logger _logger = LogManager.GetCurrentClassLogger();
    private readonly Logger _promoLogger = LogManager.GetLogger("promo");

    public async Task ProcessaApaga(string sku)
    {
        try
        {
            _client.Product.Delete(sku);
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.NotFound)
        {
            // get também faz exceção se 404, não dá pra checar antes de fazer o delete
            _logger.Info($"{sku} já estava excluído no feed, retornou 404");
        }

        await db.GoogleFeed.RemoveSkuFromFeedTables(sku);
        _logger.Info($"{sku} apagado");
    }

    public async Task InsereOuAtualiza(string sku)
    {
        /*
         * insere ou atualiza dependendo se já existe ou não na API,
         * importa só pro log diferente, pra conferir se mudou algo:
         * o que pode mudar:
         *       - preço
         *       - inStock
         *       - promoId
         *       - label
         * (atributos, detalhes etc. também, mas esses são os mais importantes pra log)
         */
        if (_client.Product.ProductExists(sku, out var oldProduct))
        {
            // atualizando existente
            var oldPromo = oldProduct!.ProductAttributes.PromotionIds.FirstOrDefault();
            var json = await _client.Product.Post(sku);
            var oldLabel = oldProduct.ProductAttributes.CustomLabel0;
            var newLabel = json.ProductAttributes.CustomLabel0;
            var oldStock = oldProduct.ProductAttributes.Availability;
            var newStock = json.ProductAttributes.Availability;
            var oldPrice = $"{oldProduct.ProductAttributes.Price.AmountMicros / 1000000:N2}";
            var newPrice = $"{json.ProductAttributes.Price.AmountMicros / 1000000:N2}";
            var newpromoId = json.ProductAttributes.PromotionIds.FirstOrDefault();
            var logStr = $"{sku} atualizando";
            if (newpromoId != oldPromo)
            {
                logStr += $" promo '{oldPromo}' => '{newpromoId}'";
                if (string.IsNullOrEmpty(newpromoId))
                {
                    await db.GoogleFeed.RemovePromotionIdDoSku(sku);
                }
                else
                {
                    await db.GoogleFeed.SalvaOuAtualizaPromotionIdDoSku(sku, newpromoId);
                }
            }
            else
            {
                logStr += $" mantendo {newpromoId}";
            }

            logStr += oldLabel != newLabel ? $" label {oldLabel} => {newLabel}" : $" mantendo label {oldLabel}";
            logStr += oldStock != newStock ? $" stock {oldStock} => {newStock}" : $" mantendo stock {oldStock}";
            logStr += oldPrice != newPrice ? $" price {oldPrice} => {newPrice}" : $" mantendo price {oldPrice}";

            _logger.Info(logStr);
            _promoLogger.Info(logStr);
        }
        else // inserindo novo
        {
            var json = await _client.Product.Post(sku);
            var promoId = json.ProductAttributes.PromotionIds.FirstOrDefault();
            var logStr = $"{sku} inserido";
            if (!string.IsNullOrEmpty(promoId))
            {
                logStr += $" com promo {promoId}";
                await db.GoogleFeed.SalvaOuAtualizaPromotionIdDoSku(sku, promoId);
            }
            else
            {
                logStr += " sem promo";
            }

            logStr += $", customLabel {json.ProductAttributes.CustomLabel0}";
            logStr += $" ({json.ProductAttributes.Availability})";
            logStr += $" R$ {(float)json.ProductAttributes.Price.AmountMicros / 1000000:N2}";
            _logger.Info(logStr);
            _promoLogger.Info(logStr);

            await db.GoogleFeed.UpdateSkuOnProductFeed(sku);
        }
    }
}