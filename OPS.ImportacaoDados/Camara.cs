﻿/*
 * Created by SharpDevelop.
 * User: Tex Killer
 * Date: 18/01/2016
 * Time: 06:23
 * 
 * To change this template use Tools | Options | Coding | Edit Standard Headers.
 */

using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Xml;
using ICSharpCode.SharpZipLib.Zip;
using OPS.Core;
using RestSharp;

namespace OPS.ImportacaoDados
{
   public static class Camara
   {
      /// <summary>
      /// </summary>
      public static void ImportarMandatos()
      {
         // http://www2.camara.leg.br/transparencia/dados-abertos/dados-abertos-legislativo/webservices/deputados
         // http://www.camara.leg.br/internet/deputado/DeputadosXML_52a55.zip

         var doc = new XmlDocument();
         doc.Load(@"C:\GitHub\operacao-politica-supervisionada\OPS\temp\DeputadosXML_52a55\Deputados.xml");
         XmlNode deputados = doc.DocumentElement;

         var deputado = deputados.SelectNodes("Deputados/Deputado");
         var sqlFields = new StringBuilder();
         var sqlValues = new StringBuilder();

         using (var banco = new Banco())
         {
            banco.ExecuteNonQuery("TRUNCATE TABLE cf_mandato_temp");

            foreach (XmlNode fileNode in deputado)
            {
               sqlFields.Clear();
               sqlValues.Clear();

               foreach (XmlNode item in fileNode.SelectNodes("*"))
               {
                  sqlFields.Append(string.Format(",{0}", item.Name));

                  sqlValues.Append(string.Format(",@{0}", item.Name));
                  banco.AddParameter(item.Name, item.InnerText.ToUpper());
               }

               banco.ExecuteNonQuery("INSERT cf_mandato_temp (" + sqlFields.ToString().Substring(1) + ")  values (" +
                                     sqlValues.ToString().Substring(1) + ")");
            }

            //TODO: Ver onde encontro uma lista de deputados com o nuDeputadoId
            //            var dtDeputadosFaltantes = banco.GetTable(@"
            //                SET SQL_BIG_SELECTS = 1;

            //                select
            //                    mt.ideCadastro
            //                FROM cf_mandato_temp mt
            //                left join cf_deputado dt on dt.id_cadastro = mt.ideCadastro
            //                where dt.id is null;

            //                SET SQL_BIG_SELECTS=0;
            //");

            //foreach (DataRow dr in dtDeputadosFaltantes.Rows)
            //{

            //}

            banco.ExecuteNonQuery(@"
                    SET SQL_BIG_SELECTS=1;

					INSERT INTO cf_mandato (
	                    id_cf_deputado,
	                    id_legislatura,
	                    id_carteira_parlamantar,
	                    id_estado, 
	                    id_partido,
                        condicao
                    )
                    select 
	                    dt.id,
	                    mt.numLegislatura,
	                    mt.Matricula,
	                    e.id, 
	                    p.id,
	                    mt.Condicao
                    FROM cf_mandato_temp mt
                    left join cf_deputado dt on dt.id_cadastro = mt.ideCadastro
                    left join partido p on p.sigla = mt.LegendaPartidoEleito
                    left join estado e on e.sigla = mt.UFEleito
                    /* Inserir apenas faltantes */
                    LEFT JOIN cf_mandato m ON m.id_cf_deputado = dt.id 
	                    AND m.id_legislatura = mt.numLegislatura 
	                    AND m.id_carteira_parlamantar = mt.Matricula
                    WHERE m.id is null
                    AND dt.id is not null;
                    
                    SET SQL_BIG_SELECTS=0;
				");

            banco.ExecuteNonQuery("TRUNCATE TABLE cf_mandato_temp;");
         }
      }

      /// <summary>
      ///     Atualiza informações dos deputados em exercício na Câmara dos Deputados
      /// </summary>
      public static void AtualizaInfoDeputados()
      {
         // http://www2.camara.leg.br/transparencia/dados-abertos/dados-abertos-legislativo/webservices/deputados

         var doc = new XmlDocument();
         var client = new RestClient("http://www.camara.leg.br/SitCamaraWS/Deputados.asmx");
         var request = new RestRequest("ObterDeputados", Method.GET);
         var response = client.Execute(request);
         doc.LoadXml(response.Content);
         XmlNode deputados = doc.DocumentElement;

         var deputado = deputados.SelectNodes("*");
         var sqlFields = new StringBuilder();
         var sqlValues = new StringBuilder();

         using (var banco = new Banco())
         {
            banco.ExecuteNonQuery("TRUNCATE TABLE cf_deputado_temp");

            foreach (XmlNode fileNode in deputado)
            {
               sqlFields.Clear();
               sqlValues.Clear();

               foreach (XmlNode item in fileNode.SelectNodes("*"))
                  if (item.Name != "comissoes")
                  {
                     sqlFields.Append(string.Format(",{0}", item.Name));

                     sqlValues.Append(string.Format(",@{0}", item.Name));
                     banco.AddParameter(item.Name, item.InnerText.ToUpper());
                  }

               banco.ExecuteNonQuery("INSERT cf_deputado_temp (" + sqlFields.ToString().Substring(1) +
                                     ")  values (" + sqlValues.ToString().Substring(1) + ")");
            }


            banco.ExecuteNonQuery(@"
                    SET SQL_BIG_SELECTS=1;

					update cf_deputado d
					left join cf_deputado_temp dt on dt.ideCadastro = d.id_cadastro
					left join partido p on p.sigla = dt.partido
					left join estado e on e.sigla = dt.uf
					set
						d.cod_orcamento = CASE dt.codOrcamento WHEN '' THEN NULL ELSE dt.codOrcamento END,
						d.condicao = dt.condicao,
						d.matricula = dt.matricula,
						d.id_parlamentar = dt.idParlamentar,
						d.nome_civil = dt.nome,
						d.nome_parlamentar = dt.nomeParlamentar,
						d.url_foto = dt.urlFoto,
						d.sexo = LEFT(dt.sexo, 1),
						d.id_estado = e.id,
						d.id_partido = p.id,
						d.gabinete = dt.gabinete,
						d.anexo = dt.anexo,
						d.fone = dt.fone,
						d.email = dt.email
					where dt.nomeParlamentar is not null;

                    SET SQL_BIG_SELECTS=0;
				");

            banco.ExecuteNonQuery("TRUNCATE TABLE cf_deputado_temp");
         }
      }

      /// <summary>
      ///     Atualiza informações dos deputados em exercício na Câmara dos Deputados
      /// </summary>
      public static void AtualizaInfoDeputadosCompleto()
      {
         // http://www2.camara.leg.br/transparencia/dados-abertos/dados-abertos-legislativo/webservices/deputados

         var sqlFields = new StringBuilder();
         var sqlValues = new StringBuilder();

         using (var banco = new Banco())
         {
            banco.ExecuteNonQuery("TRUNCATE TABLE cf_deputado_temp_detalhes");

            using (
                var dReader =
                    banco.ExecuteReader(
                        "SELECT * from cf_deputado where nome_civil is null and id_cadastro is not null and id_cadastro <> 0")
            )
            {
               using (var banco2 = new Banco())
               {
                  while (dReader.Read())
                  {
                     // Retorna detalhes dos deputados com histórico de participação em comissões, períodos de exercício, filiações partidárias e lideranças.
                     var doc = new XmlDocument();
                     var client = new RestClient("http://www.camara.leg.br/SitCamaraWS/Deputados.asmx/");
                     var request =
                         new RestRequest(
                             "ObterDetalhesDeputado?numLegislatura=&ideCadastro=" + dReader["id_cadastro"],
                             Method.GET);

                     try
                     {
                        var response = client.Execute(request);
                        doc.LoadXml(response.Content);
                     }
                     catch (Exception)
                     {
                        Thread.Sleep(5000);

                        try
                        {
                           var response = client.Execute(request);
                           doc.LoadXml(response.Content);
                        }
                        catch (Exception)
                        {
                           continue;
                        }
                     }

                     XmlNode deputados = doc.DocumentElement;
                     var deputado = deputados.SelectNodes("*");

                     foreach (XmlNode fileNode in deputado)
                     {
                        sqlFields.Clear();
                        sqlValues.Clear();

                        foreach (XmlNode item in fileNode.ChildNodes)
                        {
                           if (item.Name == "comissoes") break;
                           if ((item.Name != "partidoAtual") && (item.Name != "gabinete"))
                           {
                              object value;
                              if (item.Name.StartsWith("data"))
                                 if (!string.IsNullOrEmpty(item.InnerText))
                                    value = DateTime.Parse(item.InnerText).ToString("yyyy-MM-dd");
                                 else
                                    value = DBNull.Value;
                              else
                                 value = item.InnerText;

                              sqlFields.Append(string.Format(",{0}", item.Name));
                              sqlValues.Append(string.Format(",@{0}", item.Name));
                              banco2.AddParameter(item.Name, value);
                           }
                           else
                           {
                              foreach (XmlNode item2 in item.ChildNodes)
                              {
                                 if ((item2.Name == "idPartido") || (item2.Name == "nome")) continue;


                                 sqlFields.Append(string.Format(",{0}", item2.Name));
                                 sqlValues.Append(string.Format(",@{0}", item2.Name));
                                 banco2.AddParameter(item2.Name, item2.InnerText);
                              }
                           }
                        }

                        try
                        {
                           banco2.ExecuteNonQuery("INSERT cf_deputado_temp_detalhes (" +
                                                  sqlFields.ToString().Substring(1) + ")  values (" +
                                                  sqlValues.ToString().Substring(1) + ")");
                        }
                        catch
                        {
                           // ignored
                        }
                     }
                  }

                  banco2.ExecuteNonQuery(@"
							update cf_deputado d
							left join (
								select		
									ideCadastro,
									idParlamentarDeprecated,
									nomeCivil,
									nomeParlamentarAtual,
									sexo,
									nomeProfissao,
									dataNascimento,
									ufRepresentacaoAtual,
									max(email) as email,
									max(sigla) as sigla,
									max(numero) as numero,
									max(anexo) as anexo,
									max(telefone) as telefone,
									max(dataFalecimento) as dataFalecimento
								from cf_deputado_temp_detalhes
								group by 1, 2, 3, 4, 5, 6, 7, 8
							) dt on dt.ideCadastro = d.id_cadastro
							left join partido p on p.sigla = dt.sigla
							left join estado e on e.sigla = dt.ufRepresentacaoAtual
							set
								d.id_parlamentar = dt.idParlamentarDeprecated,
								d.nome_civil = dt.nomeCivil,
								d.nome_parlamentar = dt.nomeParlamentarAtual,
								d.sexo = LEFT(dt.sexo, 1),
								d.id_estado = e.id,
								d.id_partido = p.id,
								d.gabinete = dt.numero,
								d.anexo = dt.anexo,
								d.fone = dt.telefone,
								d.email = dt.email,
								d.profissao = dt.nomeProfissao,
								d.nascimento = dt.dataNascimento,
								d.falecimento = dt.dataFalecimento
							where dt.nomeParlamentarAtual is not null;
						");

                  banco2.ExecuteNonQuery("TRUNCATE TABLE cf_deputado_temp_detalhes;");
               }
            }
         }
      }

      public static void ImportarDespesas(string atualDir)
      {
         // http://www2.camara.leg.br/transparencia/cota-para-exercicio-da-atividade-parlamentar/dados-abertos-cota-parlamentar
         // http://www.camara.gov.br/cotas/AnosAnteriores.zip
         // http://www.camara.gov.br/cotas/AnoAnterior.zip
         // http://www.camara.gov.br/cotas/AnoAtual.zip

         var downloadUrl = "http://www.camara.leg.br/cotas/AnoAtual.zip";
         var fullFileNameZip = atualDir + @"\AnoAtual.zip";
         var fullFileNameXml = atualDir + @"\AnoAtual.xml";

         if (!Directory.Exists(atualDir))
            Directory.CreateDirectory(atualDir);

         var request = (HttpWebRequest)WebRequest.Create(downloadUrl);

         request.UserAgent = "Other";
         request.Method = "HEAD";
         request.ContentType = "application/json;charset=UTF-8";
         request.Timeout = 1000000;

         using (var resp = request.GetResponse())
         {
            var ContentLength = Convert.ToInt64(resp.Headers.Get("Content-Length"));
            var ContentLengthLocal = new FileInfo(fullFileNameZip).Length;
            if (ContentLength == ContentLengthLocal)
               return;

            using (var client = new WebClient())
            {
               client.Headers.Add("User-Agent: Other");
               client.DownloadFile(downloadUrl, fullFileNameZip);
            }
         }

         ZipFile file = null;

         try
         {
            file = new ZipFile(fullFileNameZip);

            if (file.TestArchive(true) == false)
               throw new BusinessException("<script>alert('Erro no Zip. Faça o upload novamente.')</script>");
         }
         finally
         {
            if (file != null)
               file.Close();
         }

         var zip = new FastZip();
         zip.ExtractZip(fullFileNameZip, atualDir, null);

         CarregaDadosXml(fullFileNameXml, true);

         //File.Delete(fullFileNameZip);
         //File.Delete(fullFileNameXml);
      }

      private static void CarregaDadosXml(string fullFileNameXml, bool completo)
      {
         var banco = new Banco();
         LimpaDespesaTemporaria(banco);

         //   if (completo)
         //   {
         //       banco.ExecuteNonQuery(@"
         //           DELETE FROM cf_despesa where ano=2017;

         //            -- select max(id)+1 from cf_despesa;
         //           ALTER TABLE cf_despesa AUTO_INCREMENT = 2757538;
         //");
         //   }

         StreamReader stream = null;
         var linhaAtual = 0;

         try
         {
            //if (fullFileNameXml.EndsWith("AnoAnterior.xml"))
            //    stream = new StreamReader(fullFileNameXml, Encoding.GetEncoding(850)); //"ISO-8859-1"
            //else
            stream = new StreamReader(fullFileNameXml, Encoding.GetEncoding("ISO-8859-1"));

            var sqlFields = new StringBuilder();
            var sqlValues = new StringBuilder();
            //string nuDeputadoIdControle = "";
            //int id_sequencial_deputado_ano = -1;

            using (var reader = XmlReader.Create(stream, new XmlReaderSettings { IgnoreComments = true }))
            {
               reader.ReadToDescendant("DESPESAS");
               reader.ReadToDescendant("DESPESA");

               //string tretas;
               do
               {
                  var strXmlNodeDespeza = reader.ReadOuterXml();
                  //if (strXmlNodeDespeza == "")
                  //{
                  //   Console.WriteLine(linhaAtual);
                  //   break;
                  //}
                  //else 
                  if (string.IsNullOrEmpty(strXmlNodeDespeza))
                  {
                     Console.WriteLine(linhaAtual);
                     break;
                  }
                  //else
                  //{
                  //   tretas = strXmlNodeDespeza;
                  //}

                  //DataRow drDespesa = dtDespesa.NewRow();
                  var doc = new XmlDocument();
                  doc.LoadXml(strXmlNodeDespeza);
                  var files = doc.DocumentElement.SelectNodes("*");

                  sqlFields.Clear();
                  sqlValues.Clear();

                  foreach (XmlNode fileNode in files)
                  {
                     if (sqlFields.Length > 0)
                     {
                        sqlFields.Append(",");
                        sqlValues.Append(",");
                     }

                     sqlFields.Append(fileNode.Name);
                     sqlValues.Append("@" + fileNode.Name);

                     string value;
                     if (fileNode.Name == "datEmissao")
                        value = string.IsNullOrEmpty(fileNode.InnerText) ? null : DateTime.Parse(fileNode.InnerText).ToString("yyyy-MM-dd");
                     else
                        value = string.IsNullOrEmpty(fileNode.InnerText) ? null : fileNode.InnerText.ToUpper();

                     banco.AddParameter(fileNode.Name, value);
                  }

                  banco.ExecuteNonQuery("INSERT INTO cf_despesa_temp (" + sqlFields + ") VALUES (" + sqlValues + ")");

                  if (++linhaAtual == 10000)
                  {
                     Console.WriteLine(linhaAtual);

                     linhaAtual = 0;
                     banco.Dispose();
                     banco = new Banco();

                     if (completo)
                        ProcessarDespesasTemp(banco, completo);
                  }
               } while (true);

               reader.Close();
            }
         }
         finally
         {
            banco.Dispose();

            stream.Close();
            stream.Dispose();
         }

         banco = new Banco();
         ProcessarDespesasTemp(banco, completo);

         banco.ExecuteNonQuery(@"
				    UPDATE parametros SET cf_deputado_ultima_atualizacao=NOW();
			    ");
         banco.Dispose();

         AtualizaDeputadoValores();
      }

      private static void ProcessarDespesasTemp(Banco banco, bool completo)
      {
         CorrigeDespesas(banco);
         InsereDeputadoFaltante(banco);
         InsereTipoDespesaFaltante(banco);
         InsereTipoEspecificacaoFaltante(banco);
         InsereMandatoFaltante(banco);
         InsereLegislaturaFaltante(banco);
         InsereFornecedorFaltante(banco);


         if (completo)
            InsereDespesaFinal(banco);
         else
            InsereDespesaFinalParcial(banco);

         LimpaDespesaTemporaria(banco);
      }

      private static void CorrigeDespesas(Banco banco)
      {
         banco.ExecuteNonQuery(@"
				UPDATE cf_despesa_temp SET txtNumero = NULL WHERE txtNumero = 'S/N' OR txtNumero = '';
				UPDATE cf_despesa_temp SET ideDocumento = NULL WHERE ideDocumento = '';
				UPDATE cf_despesa_temp SET txtCNPJCPF = NULL WHERE txtCNPJCPF = '';
				UPDATE cf_despesa_temp SET txtFornecedor = 'CORREIOS' WHERE txtCNPJCPF is null and txtFornecedor LIKE 'CORREIOS%';	
			");
      }

      private static void InsereDeputadoFaltante(Banco banco)
      {
         banco.ExecuteNonQuery(@"
				INSERT INTO cf_deputado (id, id_cadastro, nome_parlamentar)
				select distinct nuDeputadoId, ideCadastro, txNomeParlamentar 
				from cf_despesa_temp
				where nuDeputadoId  not in (
					select id from cf_deputado
				);
			");
      }

      private static void InsereTipoDespesaFaltante(Banco banco)
      {
         banco.ExecuteNonQuery(@"
				INSERT INTO cf_despesa_tipo (id, descricao)
				select distinct numSubCota, txtDescricao
				from cf_despesa_temp
				where numSubCota  not in (
					select id from cf_despesa_tipo
				);
			");
      }

      private static void InsereTipoEspecificacaoFaltante(Banco banco)
      {
         banco.ExecuteNonQuery(@"
				INSERT INTO cf_especificacao_tipo (id_cf_despesa_tipo, id_cf_especificacao, descricao)
				select distinct numSubCota, numEspecificacaoSubCota, txtDescricaoEspecificacao
				from cf_despesa_temp dt
				left join cf_especificacao_tipo tp on tp.id_cf_despesa_tipo = dt.numSubCota 
					and tp.id_cf_especificacao = dt.numEspecificacaoSubCota
				where numEspecificacaoSubCota <> 0
				AND tp.descricao = null;
			");
      }

      private static void InsereMandatoFaltante(Banco banco)
      {
         banco.ExecuteNonQuery(@"
                SET SQL_BIG_SELECTS=1;

				INSERT INTO cf_mandato (id_cf_deputado, id_legislatura, id_carteira_parlamantar, id_estado, id_partido)
				select distinct nuDeputadoId, codLegislatura, nuCarteiraParlamentar, e.id, p.id 
				from ( 
					select distinct 
					nuDeputadoId, nuLegislatura, nuCarteiraParlamentar, codLegislatura, sgUF, sgPartido
					from cf_despesa_temp
				) dt
				left join estado e on e.sigla = dt.sgUF
				left join partido p on p.sigla = dt.sgPartido
				left join cf_mandato m on m.id_cf_deputado = dt.nuDeputadoId 
					AND m.id_legislatura = dt.codLegislatura 
					AND m.id_carteira_parlamantar = nuCarteiraParlamentar
				where dt.codLegislatura <> 0
				AND m.id is null;

                SET SQL_BIG_SELECTS=0;
			");
      }

      private static void InsereLegislaturaFaltante(Banco banco)
      {
         banco.ExecuteNonQuery(@"
				INSERT INTO cf_legislatura (id, ano)
				select distinct codLegislatura, nuLegislatura
				from cf_despesa_temp dt
				where codLegislatura <> 0
				AND codLegislatura  not in (
					select id from cf_legislatura
				);
			");
      }

      private static void InsereFornecedorFaltante(Banco banco)
      {
         banco.ExecuteNonQuery(@"
                SET SQL_BIG_SELECTS=1;

				INSERT INTO fornecedor (nome, cnpj_cpf)
				select MAX(dt.txtFornecedor), dt.txtCNPJCPF
				from cf_despesa_temp dt
				left join fornecedor f on f.cnpj_cpf = dt.txtCNPJCPF
				where dt.txtCNPJCPF is not null
				and f.id is null
				GROUP BY dt.txtCNPJCPF;

				INSERT INTO fornecedor (nome, cnpj_cpf)
				select DISTINCT dt.txtFornecedor, dt.txtCNPJCPF
				from cf_despesa_temp dt
				left join fornecedor f on f.nome = dt.txtFornecedor
				where dt.txtCNPJCPF is null
				and f.id is null;

                SET SQL_BIG_SELECTS=0;
			");
      }

      private static void InsereDespesaFinal(Banco banco)
      {
         banco.ExecuteNonQuery(@"
                SET SQL_BIG_SELECTS=1;
				 ALTER TABLE cf_despesa DISABLE KEYS;

				INSERT INTO cf_despesa (
					id_documento,
					id_cf_deputado,
					id_cf_mandato,
					id_cf_despesa_tipo,
					id_cf_especificacao,
					id_fornecedor,
					nome_passageiro,
					numero_documento,
					tipo_documento,
					data_emissao,
					valor_documento,
					valor_glosa,
					valor_liquido,
					valor_restituicao,
					mes,
					ano,
					parcela,
					trecho_viagem,
					lote,
					ressarcimento,
					ano_mes
				)
				select 
					dt.ideDocumento,
					dt.nuDeputadoId,
					m.id,
					numSubCota,
					numEspecificacaoSubCota,
					f.id,
					txtPassageiro, -- id_passageiro,
					txtNumero,
					indTipoDocumento,
					datEmissao,
					vlrDocumento,
					vlrGlosa,
					vlrLiquido,
					vlrRestituicao,
					numMes,
					numAno,
					numParcela,
					txtTrecho,
					numLote,
					numRessarcimento,
					concat(numAno, LPAD(numMes, 2, '0'))
				from cf_despesa_temp dt
				LEFT join fornecedor f on f.cnpj_cpf = dt.txtCNPJCPF
					or (f.cnpj_cpf is null and dt.txtCNPJCPF is null and f.nome = dt.txtFornecedor)
				left join cf_mandato m on m.id_cf_deputado = dt.nuDeputadoId
					and m.id_legislatura = dt.codLegislatura 
					and m.id_carteira_parlamantar = nuCarteiraParlamentar;
    
				ALTER TABLE cf_despesa ENABLE KEYS;
                SET SQL_BIG_SELECTS=0;
			", 3600);
      }

      private static void InsereDespesaFinalParcial(Banco banco)
      {
         var dt = banco.GetTable(
             @"SET SQL_BIG_SELECTS=1;

                DROP TABLE IF EXISTS table_in_memory_d;
                CREATE TEMPORARY TABLE table_in_memory_d
                AS (
	                select id_cf_deputado, mes, sum(valor_liquido) as total
	                from cf_despesa d
	                where ano = 2017
	                GROUP BY id_cf_deputado, mes
                );

                DROP TABLE IF EXISTS table_in_memory_dt;
                CREATE TEMPORARY TABLE table_in_memory_dt
                AS (
	                select nuDeputadoId, numMes, sum(vlrLiquido) as total
	                from cf_despesa_temp d
	                GROUP BY nuDeputadoId, numMes
                );

                select dt.nuDeputadoId as id_cf_deputado, dt.numMes as mes
                from table_in_memory_dt dt
                left join table_in_memory_d d on dt.nuDeputadoId = d.id_cf_deputado and dt.numMes = d.mes
                where (d.id_cf_deputado or d.total <> dt.total)
                order by d.id_cf_deputado, d.mes;

                SET SQL_BIG_SELECTS=0;
			    ", 3600);

         foreach (DataRow dr in dt.Rows)
         {
            banco.AddParameter("id_cf_deputado", dr["id_cf_deputado"]);
            banco.AddParameter("mes", dr["mes"]);
            banco.ExecuteNonQuery(
                @"DELETE FROM cf_despesa WHERE id_cf_deputado=@id_cf_deputado and ano=2017 and mes=@mes");

            banco.AddParameter("id_cf_deputado", dr["id_cf_deputado"]);
            banco.AddParameter("mes", dr["mes"]);
            banco.ExecuteNonQuery(@"
                        SET SQL_BIG_SELECTS=1;

				        INSERT INTO cf_despesa (
					        id_documento,
					        id_cf_deputado,
					        id_cf_mandato,
					        id_cf_despesa_tipo,
					        id_cf_especificacao,
					        id_fornecedor,
					        nome_passageiro,
					        numero_documento,
					        tipo_documento,
					        data_emissao,
					        valor_documento,
					        valor_glosa,
					        valor_liquido,
					        valor_restituicao,
					        mes,
					        ano,
					        parcela,
					        trecho_viagem,
					        lote,
					        ressarcimento,
					        ano_mes
				        )
				        select 
						    dt.ideDocumento,
						    dt.nuDeputadoId,
						    m.id,
						    numSubCota,
						    numEspecificacaoSubCota,
						    f.id,
						    txtPassageiro, -- id_passageiro,
						    txtNumero,
						    indTipoDocumento,
						    datEmissao,
						    vlrDocumento,
						    vlrGlosa,
						    vlrLiquido,
						    vlrRestituicao,
						    numMes,
						    numAno,
						    numParcela,
						    txtTrecho,
						    numLote,
						    numRessarcimento,
						    concat(numAno, LPAD(numMes, 2, '0'))
					    from (
						    select *					        
						    from cf_despesa_temp dt
						    WHERE nuDeputadoId=@id_cf_deputado and numMes=@mes
					    ) dt
				        LEFT join fornecedor f on f.cnpj_cpf = dt.txtCNPJCPF
					        or (f.cnpj_cpf is null and dt.txtCNPJCPF is null and f.nome = dt.txtFornecedor)
				        left join cf_mandato m on m.id_cf_deputado = dt.nuDeputadoId
					        and m.id_legislatura = dt.codLegislatura 
					        and m.id_carteira_parlamantar = nuCarteiraParlamentar;

                        SET SQL_BIG_SELECTS=0;
			        ", 3600);
         }
      }

      private static void LimpaDespesaTemporaria(Banco banco)
      {
         banco.ExecuteNonQuery(@"

                truncate table cf_despesa_temp;
			");
      }

      public static void ImportaPresencasDeputados()
      {
         var sqlFields = new StringBuilder();
         var sqlValues = new StringBuilder();

         //Carregar a partir da legislatura 53. Existem dados desde 24/02/99
         var dtPesquisa = new DateTime(2015, 1, 31); //new DateTime(2007, 2, 4);

         DataTable dtMandatos;
         using (var banco = new Banco())
         {
            dtMandatos =
                banco.GetTable("select id_cf_deputado, id_legislatura, id_carteira_parlamantar from cf_mandato");
         }

         var datetimenow = DateTime.Now.Date;

         while (true)
         {
            dtPesquisa = dtPesquisa.AddDays(1);
            if (dtPesquisa == datetimenow)
               break;

            var doc = new XmlDocument();
            IRestResponse response;
            var client = new RestClient("http://www.camara.leg.br/SitCamaraWS/sessoesreunioes.asmx/");
            var request =
                new RestRequest(
                    "ListarPresencasDia?data={data}&numLegislatura=&numMatriculaParlamentar=&siglaPartido=&siglaUF=",
                    Method.GET);

            request.AddUrlSegment("data", dtPesquisa.ToString("dd/MM/yyyy"));

            try
            {
               response = client.Execute(request);
               if (!response.Content.Contains("qtdeSessoesDia"))
                  continue;

               doc.LoadXml(response.Content);
            }
            catch
            {
               Thread.Sleep(5000);

               response = client.Execute(request);
               doc.LoadXml(response.Content);
            }

            XmlNode dia = doc.DocumentElement;

            var lstSessaoDia = new Dictionary<string, int>();

            using (var banco = new Banco())
            {
               var count = Convert.ToInt32(dia["qtdeSessoesDia"].InnerText);
               for (var i = 0; i < count; i++)
               {
                  var temp = dia.SelectNodes("parlamentares/parlamentar/sessoesDia/sessaoDia").Item(i);

                  var inicio = temp["inicio"].InnerText;

                  if (lstSessaoDia.ContainsKey(inicio)) continue; // ignorar registro duplicado

                  var descricao =
                      temp["descricao"].InnerText
                          .Replace("N º", "").Replace("Nº", "")
                          .Replace("SESSÃO PREPARATÓRIA", "SESSÃO_PREPARATÓRIA")
                          .Split(new[] { '-', ' ' }, StringSplitOptions.RemoveEmptyEntries);

                  banco.AddParameter("legislatura", dia["legislatura"].InnerText);
                  banco.AddParameter("data", DateTime.Parse(dia["data"].InnerText));
                  banco.AddParameter("inicio", DateTime.Parse(inicio));

                  //1=> ORDINÁRIA, 2=> EXTRAORDINÁRIA, 3=> SESSÃO PREPARATÓRIA
                  var tipo = 0;
                  switch (descricao[0])
                  {
                     case "ORDINÁRIA":
                     case "ORDINARIA":
                        tipo = 1;
                        break;
                     case "EXTRAORDINÁRIA":
                     case "EXTRAORDINARIA":
                        tipo = 2;
                        break;
                     case "SESSÃO_PREPARATÓRIA":
                     case "SESSÃO_PREPARATORIA":
                        tipo = 3;
                        break;
                     default:
                        throw new NotImplementedException("");
                  }

                  banco.AddParameter("tipo", tipo);
                  banco.AddParameter("numero", descricao[1]);

                  var id_cf_secao = Convert.ToInt32(banco.ExecuteScalar(
                      "INSERT cf_sessao (legislatura, data, inicio, tipo, numero) values (@legislatura, @data, @inicio, @tipo, @numero); SELECT LAST_INSERT_ID();"));

                  lstSessaoDia.Add(inicio, id_cf_secao);
               }

               var parlamentares = dia.SelectNodes("parlamentares/parlamentar");

               foreach (XmlNode parlamentar in parlamentares)
               {
                  var sessoesDia = parlamentar.SelectNodes("sessoesDia/sessaoDia");

                  foreach (XmlNode sessaoDia in sessoesDia)
                  {
                     string id_cf_deputado;
                     var drMqandato = dtMandatos.Select(
                         string.Format("id_legislatura={0} and id_carteira_parlamantar={1}",
                             dia["legislatura"].InnerText,
                             parlamentar["carteiraParlamentar"].InnerText
                         )
                     );

                     if (drMqandato.Length > 0)
                     {
                        id_cf_deputado = drMqandato[0]["id_cf_deputado"].ToString();
                     }
                     else
                     {
                        var nomeparlamentar = parlamentar["nomeParlamentar"].InnerText.Split('-');
                        var sigla_partido = "";
                        var sigla_estado = "";
                        try
                        {
                           var temp = nomeparlamentar[1].Split('/');
                           sigla_partido = temp[0];
                           sigla_estado = temp[1];
                        }
                        catch (Exception e)
                        {
                           var x = e;
                        }

                        banco.AddParameter("nome_parlamentar", nomeparlamentar[0]);
                        banco.AddParameter("id_legislatura", dia["legislatura"].InnerText);
                        banco.AddParameter("id_carteira_parlamantar",
                            parlamentar["carteiraParlamentar"].InnerText);
                        banco.AddParameter("sigla_estado", sigla_estado);
                        banco.AddParameter("sigla_partido", sigla_partido);

                        try
                        {
                           id_cf_deputado =
                               banco.ExecuteScalar(
                                   @"INSERT INTO cf_mandato (
	                                        id_cf_deputado, id_legislatura, id_carteira_parlamantar, id_estado, id_partido
                                        ) VALUES (
	                                        (SELECT id FROM cf_deputado where nome_parlamentar like @nome_parlamentar)
                                            , @id_legislatura
                                            , @id_carteira_parlamantar
                                            , (SELECT id FROM estado where sigla like @sigla_estado)
                                            , (SELECT id FROM partido where sigla like @sigla_partido)
                                        );

                                        SELECT LAST_INSERT_ID();"
                               ).ToString();

                           // generate the data you want to insert
                           var toInsert = dtMandatos.NewRow();
                           toInsert["id_cf_deputado"] = id_cf_deputado;
                           toInsert["id_legislatura"] = dia["legislatura"].InnerText;
                           toInsert["id_carteira_parlamantar"] = parlamentar["carteiraParlamentar"].InnerText;

                           // insert in the desired place
                           dtMandatos.Rows.Add(toInsert);
                        }
                        catch (Exception)
                        {
                           // parlamentar não existe na base
                           Console.WriteLine(parlamentar["nomeParlamentar"].InnerText + "/" +
                                             dia["legislatura"].InnerText + "/" +
                                             parlamentar["carteiraParlamentar"].InnerText);
                           break;
                        }
                     }


                     banco.AddParameter("id_cf_sessao", lstSessaoDia[sessaoDia["inicio"].InnerText]);
                     banco.AddParameter("id_cf_deputado", Convert.ToInt32(id_cf_deputado));

                     banco.AddParameter("presente", sessaoDia["frequencia"].InnerText == "Presença" ? 1 : 0);
                     banco.AddParameter("justificativa", parlamentar["justificativa"].InnerText);
                     banco.AddParameter("presenca_externa", parlamentar["presencaExterna"].InnerText);

                     banco.ExecuteNonQuery(
                         "INSERT cf_sessao_presenca (id_cf_sessao, id_cf_deputado, presente, justificativa, presenca_externa) values (@id_cf_sessao, @id_cf_deputado, @presente, @justificativa, @presenca_externa);");
                  }
               }
            }
         }
      }

      public static void AtualizaDeputadoValores()
      {
         using (var banco = new Banco())
         {
            var dt = banco.GetTable("select id, id_cadastro from cf_deputado");
            object quantidade_secretarios;
            object valor_total_ceap;

            foreach (DataRow dr in dt.Rows)
            {
               banco.AddParameter("id_cf_deputado", dr["id"]);
               valor_total_ceap =
                   banco.ExecuteScalar(
                       "select sum(valor_liquido) from cf_despesa where id_cf_deputado=@id_cf_deputado;");

               if (!Convert.IsDBNull(dr["id_cadastro"]))
               {
                  banco.AddParameter("id_cadastro_deputado", dr["id_cadastro"]);
                  quantidade_secretarios =
                      banco.ExecuteScalar(
                          "select count(1) from cf_secretario where id_cadastro_deputado=@id_cadastro_deputado;");
               }
               else
               {
                  quantidade_secretarios = 0;
               }

               banco.AddParameter("quantidade_secretarios", quantidade_secretarios);
               banco.AddParameter("valor_total_ceap", valor_total_ceap);
               banco.AddParameter("id_cf_deputado", dr["id"]);
               banco.ExecuteNonQuery(
                   @"update cf_deputado set 
						quantidade_secretarios=@quantidade_secretarios
						, valor_total_ceap=@valor_total_ceap 
						where id=@id_cf_deputado"
               );
            }
         }
      }
   }
}