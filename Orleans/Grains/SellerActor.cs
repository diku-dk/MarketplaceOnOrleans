using System;
using Common.Entities;
using Microsoft.Extensions.Logging;
using Orleans.Interfaces;
using Orleans.Runtime;

namespace Orleans.Grains
{
	public class SellerActor : Grain, ISellerActor
    {
        private readonly ILogger<SellerActor> _logger;

        public SellerActor(
            ILogger<SellerActor> _logger)
        {
            this._logger = _logger;
        }

        public Task IndexProduct(int product_id)
        {
            return Task.CompletedTask;
        }
    }
}

