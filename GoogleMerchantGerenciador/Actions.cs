using ControllerDBApi;
using GoogleMerchantApi;
using Grpc.Core;
using NLog;

namespace GoogleMerchantGerenciador;

public class Actions(ControllerDB db)
{
    private readonly MerchantClient _client = new();
    private readonly Logger _logger = LogManager.GetCurrentClassLogger();
    private readonly Logger _loggerPromo = LogManager.GetLogger("promo");
    private readonly Logger _loggerPrice = LogManager.GetLogger("price");
    private readonly Logger _loggerStock = LogManager.GetLogger("stock");
    private readonly Logger _loggerLabel = LogManager.GetLogger("label");

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

            var logStr = $"{sku} atualizando ";
            if (newpromoId != oldPromo)
            {
                var promoLog = $"promo '{oldPromo}' => '{newpromoId}' ";
                logStr += promoLog;
                _loggerPromo.Info(sku + ": " + promoLog);
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
                logStr += $" mantendo promo {newpromoId}";
            }

            if (oldLabel != newLabel)
            {
                var labelLog = $" label {oldLabel} => {newLabel} ";
                logStr += labelLog;
                _loggerLabel.Info(sku + ": " + labelLog);
            }
            else
            {
                logStr += $" mantendo label {oldLabel} ";
            }

            if (oldStock != newStock)
            {
                var stockLog = $" stock {oldStock} => {newStock} ";
                logStr += stockLog;
                _loggerStock.Info(sku + ": " + stockLog);
            }
            else
            {
                logStr += $" mantendo stock {oldStock} ";
            }

            if (oldPrice != newPrice)
            {
                var priceLog = $" price {oldPrice} => {newPrice} ";
                logStr += priceLog;
                _loggerPrice.Info(sku + ": " + priceLog);
            }
            else
            {
                logStr += $" mantendo price {oldPrice} ";
            }

            _logger.Info(logStr);
        }
        else // inserindo novo
        {
            var json = await _client.Product.Post(sku);
            var promoId = json.ProductAttributes.PromotionIds.FirstOrDefault();
            var logStr = $"{sku} inserido";
            if (!string.IsNullOrEmpty(promoId))
            {
                var promolog = $" com promo {promoId}";
                _loggerPromo.Info(sku + promolog);
                logStr += promolog;
                await db.GoogleFeed.SalvaOuAtualizaPromotionIdDoSku(sku, promoId);
            }
            else
            {
                var promolog = " sem promo";
                _loggerPromo.Info(sku + promolog);
                logStr += promolog;
            }

            _loggerLabel.Info(sku + "label: " + json.ProductAttributes.CustomLabel0);
            logStr += $", customLabel {json.ProductAttributes.CustomLabel0}";
            _loggerStock.Info(sku + "stock: " +  json.ProductAttributes.Availability);
            logStr += $" ({json.ProductAttributes.Availability})";
            _loggerPrice.Info(sku + "price: " + $" R$ {(float)json.ProductAttributes.Price.AmountMicros / 1000000:N2}");
            logStr += $" R$ {(float)json.ProductAttributes.Price.AmountMicros / 1000000:N2}";
            _logger.Info(logStr);

            await db.GoogleFeed.UpdateSkuOnProductFeed(sku);
        }
    }
}