using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using AutoMapper;
using DevIO.Api.Extensions;
using DevIO.Api.ViewModels;
using DevIO.Business.Intefaces;
using DevIO.Business.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DevIO.Api.Controllers
{
    [Authorize]
    [Route("api/produtos")]
    public  class ProdutosController : MainController
    {

        private readonly IFornecedorRepository _fornecedorRepository;
        private readonly IProdutoRepository _produtoRepository;
        private readonly IProdutoService _produtoService;
        private readonly IMapper _mapper;
        private readonly IFornecedorService _fornecedorService;
        private readonly IEnderecoRepository _enderecoRepository;

        public ProdutosController(IFornecedorRepository fornecedorRepository,
                                        IMapper mapper,
                                        IFornecedorService fornecedorService,
                                        INotificador notificador,
                                        IEnderecoRepository enderecoRepository,
                                        IProdutoRepository produtoRepository,
                                        IProdutoService produtoService,
                                        IUser user) : base(notificador, user)
        {
            _fornecedorRepository = fornecedorRepository;
            _mapper = mapper;
            _fornecedorService = fornecedorService;
            _enderecoRepository = enderecoRepository;
            _produtoRepository = produtoRepository;
            _produtoService =  produtoService;
        }

        [HttpGet]
        public async Task<IEnumerable<ProdutoViewModel>> ObterTodos()
        {
            return _mapper.Map<IEnumerable<ProdutoViewModel>>(await _produtoRepository.ObterTodos()) ;
        }

        [HttpGet("{id:guid}")]
        public async Task<ActionResult<ProdutoViewModel>> ObterPorId(Guid id)
        {
            var produtoViewModel = await ObterProduto(id);

            if (produtoViewModel == null) return NotFound();

            return produtoViewModel;

        }

        [ClaimsAuthorize("Produto", "Excluir")]
        [HttpDelete("{id:guid}")]
        public async Task<ActionResult<ProdutoViewModel>> Excluir(Guid id)
        {
            var produtoViewModel = await this.ObterProduto(id);

            if (produtoViewModel == null) return NotFound();

            await _produtoService.Remover(id);

            return CustomResponse(produtoViewModel);
        }

        [ClaimsAuthorize("Produto","Adicionar")]
        [HttpPost]
        public async Task<ActionResult<ProdutoViewModel>> Adicionar(ProdutoViewModel produtoViewModel)
        {

            if (!ModelState.IsValid) return CustomResponse(ModelState);

            var imagemNome = Guid.NewGuid() + "-" + produtoViewModel.Imagem;

            if (!UploadArquivo(produtoViewModel.ImagemUpload, imagemNome))
            {
                return CustomResponse(produtoViewModel);
            }

            produtoViewModel.Imagem = imagemNome;

            await _produtoService.Adicionar(_mapper.Map<Produto>(produtoViewModel));

            return CustomResponse(produtoViewModel);
        }

        [ClaimsAuthorize("Produto", "Adicionar")]
        //[DisableRequestSizeLimit]
        [HttpPost("Adicionar")]
        public async Task<ActionResult<ProdutoImagemViewModel>> AdicionarAlternativo(ProdutoImagemViewModel produtoViewModel)
        {

            if (!ModelState.IsValid) return CustomResponse(ModelState);

            var imgPrefixo = Guid.NewGuid() + "_";

            if (!await UploadArquivoAlternativo(produtoViewModel.ImagemUpload, imgPrefixo))
            {
                return CustomResponse(produtoViewModel);
            }

            produtoViewModel.Imagem = imgPrefixo + produtoViewModel.ImagemUpload.FileName ;

            await _produtoService.Adicionar(_mapper.Map<Produto>(produtoViewModel));

            return CustomResponse(produtoViewModel);
        }

        [DisableRequestSizeLimit]
        [HttpPost("imagem")]
        public async Task<ActionResult> AdicionarImagem(IFormFile file)
        {

            return Ok(file);
        }



        private async Task<ProdutoViewModel> ObterProduto(Guid id)
        {
            return _mapper.Map<ProdutoViewModel>(await _produtoRepository.ObterProdutoFornecedor(id));
        }

        private bool UploadArquivo(string arquivo, string imgNome)
        {          

            if(string.IsNullOrEmpty(arquivo))
            {
                ModelState.AddModelError(string.Empty, "Forneça uma imagem para esse produto");
                return false;
            }

            var imageDataBitArry = Convert.FromBase64String(arquivo);

            var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/app/demo-webapi/src/assets", imgNome);

            if (System.IO.File.Exists(filePath))
            {
                ModelState.AddModelError(string.Empty, "Já existe um arquivo com esse nome");
            }

            System.IO.File.WriteAllBytes(filePath, imageDataBitArry);

            return true;
        }

        private async Task<bool> UploadArquivoAlternativo(IFormFile arquivo, string imgPrefixo)
        {
            if(arquivo == null || arquivo.Length <=0)
            {
                NotificarErro("Forneça uma imagem");
                return false;
            }

            var path = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/app/demo-webapi/src/assets", imgPrefixo + arquivo.FileName);

            if (System.IO.File.Exists(path))
            {
                NotificarErro("Já existe um arquivo com esse nome");
                return false;
            }            

            using (var stream = new FileStream(path, FileMode.Create))
            {
                await arquivo.CopyToAsync(stream);
            }            

            return true;
        }

        [ClaimsAuthorize("Produto", "Atualizar")]
        [HttpPut("{id:guid}")]
        public async Task<ActionResult> Atualizar(Guid id, ProdutoViewModel produtoViewModel)
        {
            if (id != produtoViewModel.Id)
            {
                NotificarErro("O Id não correponde ao objeto");

                return CustomResponse(produtoViewModel);
            }

            var produtoAtualizacao = await ObterProduto(id);

            produtoViewModel.Imagem = produtoAtualizacao.Imagem;

            if (!ModelState.IsValid) return CustomResponse(ModelState);

            if(produtoViewModel.ImagemUpload != null)
            {
                var imagemNome = Guid.NewGuid() + "_" + produtoViewModel.Imagem;

                if (!UploadArquivo(produtoViewModel.ImagemUpload, imagemNome))
                {
                    return CustomResponse(ModelState);
                }

                produtoAtualizacao.Imagem = imagemNome;
            }

            produtoAtualizacao.Nome = produtoViewModel.Nome;
            produtoAtualizacao.Descricao = produtoViewModel.Descricao;
            produtoAtualizacao.Valor = produtoViewModel.Valor;
            produtoAtualizacao.Ativo = produtoViewModel.Ativo;            

            await _produtoRepository.Atualizar(_mapper.Map<Produto>(produtoAtualizacao));

            return CustomResponse(produtoViewModel);
        }



        //public async Task<ActionResult<FornecedorViewModel>> ObterFornecedorProdutosEndereco(Guid id)
        //{
        //    var fornecedor = _mapper.Map<FornecedorViewModel>(await _fornecedorRepository.ObterFornecedorProdutosEndereco(id));

        //    return Ok(fornecedor);
        //}






        //[HttpGet("obter-endereco/{id:guid}")]
        //public async Task<EnderecoViewModel> ObterEnderecoPorId(Guid id)
        //{
        //    var enderecoViewModel = _mapper.Map<EnderecoViewModel>(await _enderecoRepository.ObterPorId(id));

        //    return enderecoViewModel;

        //}

        //[HttpPut("atualizar-endereco/{id:guid}")]
        //public async Task<ActionResult<FornecedorViewModel>> AtualizarEndereco(Guid id, EnderecoViewModel enderecoViewModel)
        //{
        //    if (id != enderecoViewModel.Id)
        //    {
        //        NotificarErro("O Id não correponde ao objeto");

        //        return CustomResponse(enderecoViewModel);
        //    }

        //    if (!ModelState.IsValid) return CustomResponse(ModelState);

        //    var fornecedor = _mapper.Map<Endereco>(enderecoViewModel);

        //    await _fornecedorService.AtualizarEndereco(_mapper.Map<Endereco>(enderecoViewModel));

        //    return CustomResponse(enderecoViewModel);
        //}

        //Validação de notificações de erro


        //Validações de ModelState


        //Validação da operação de negócios

    }
}
