using ControllerDBApi;
using GoogleMerchantApi;
using Grpc.Core;
using NLog;

namespace GoogleMerchantGerenciador;

public class Actions(ControllerDB db)
{
    private readonly MerchantClient _client = new();
    private readonly Logger _logger = LogManager.GetCurrentClassLogger();

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

    public async Task ProcessaInsere(string sku)
    {
        if (_client.Product.ProductExists(sku))
        {
            // await Atualiza(sku);
        }
        else
        {
            await ProcessaInsere(sku);
        }
    }

    public async Task ProcessaAtualiza(string sku)
    {
        if (_client.Product.ProductExists(sku))
        {
            await ProcessaAtualiza(sku);
        }
        else
        {
            await ProcessaInsere(sku);
        }
    }

    public async Task Insere(string sku)
    {
    }
}