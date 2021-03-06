﻿using MySql.Data.MySqlClient;
using OPS.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Web;

namespace OPS.Dao
{
	public class SenadorDao
	{
		internal dynamic Consultar(int id)
		{
			using (Banco banco = new Banco())
			{
				var strSql = @"
					SELECT 
						d.id as id_sf_senador
						, d.nome as nome_parlamentar
						, e.sigla as sigla_estado
						, p.sigla as sigla_partido
						, d.url
						, d.valor_total_ceaps
					FROM sf_senador d
					LEFT JOIN partido p on p.id = d.id_partido
					LEFT JOIN estado e on e.id = d.id_estado
					WHERE d.id = @id
				";
				banco.AddParameter("@id", id);

				using (MySqlDataReader reader = banco.ExecuteReader(strSql))
				{
					if (reader.Read())
					{
						return new
						{
							id_sf_senador = reader["id_sf_senador"],
							nome_parlamentar = reader["nome_parlamentar"].ToString(),
							sigla_estado = reader["sigla_estado"].ToString(),
							sigla_partido = reader["sigla_partido"].ToString(),
							url = reader["url"].ToString(),

							valor_total_ceaps = Utils.FormataValor(reader["valor_total_ceaps"]),
						};
					}

					return null;
				}
			}
		}

		internal dynamic MaioresFornecedores(int id)
		{
			using (Banco banco = new Banco())
			{
				var strSql = new StringBuilder();

				strSql.AppendLine(@"
					SELECT
						 pj.id as id_fornecedor
						, pj.cnpj_cpf
						, pj.nome AS nome_fornecedor
						, l1.valor_total
					from (
						SELECT
							l.id_fornecedor
							, SUM(l.valor) as valor_total
						FROM sf_despesa l
						WHERE l.id_sf_senador = @id
						GROUP BY l.id_fornecedor
						order by valor_total desc
						LIMIT 10
					) l1
					LEFT JOIN fornecedor pj on pj.id = l1.id_fornecedor
					order by l1.valor_total desc
				");

				banco.AddParameter("@id", id);

				using (MySqlDataReader reader = banco.ExecuteReader(strSql.ToString()))
				{
					List<dynamic> lstRetorno = new List<dynamic>();
					while (reader.Read())
					{
						lstRetorno.Add(new
						{
							id_fornecedor = reader["id_fornecedor"].ToString(),
							cnpj_cpf = reader["cnpj_cpf"].ToString(),
							nome_fornecedor = reader["nome_fornecedor"].ToString(),
							valor_total = Utils.FormataValor(reader["valor_total"])
						});
					}

					return lstRetorno;
				}
			}
		}

		internal dynamic MaioresNotas(int id)
		{
			using (Banco banco = new Banco())
			{
				var strSql = new StringBuilder();

				strSql.AppendLine(@"
					SELECT
						 l1.id as id_sf_despesa
						, l1.id_fornecedor
						, pj.cnpj_cpf
						, pj.nome AS nome_fornecedor
						, l1.valor
					from (
						SELECT
						l.id
						, l.valor
						, l.id_fornecedor
						FROM sf_despesa l
						WHERE l.id_sf_senador = @id
						order by l.valor desc
						LIMIT 10
					) l1
					LEFT JOIN fornecedor pj on pj.id = l1.id_fornecedor
					order by l1.valor desc 
				");

				banco.AddParameter("@id", id);

				using (MySqlDataReader reader = banco.ExecuteReader(strSql.ToString()))
				{
					List<dynamic> lstRetorno = new List<dynamic>();
					while (reader.Read())
					{
						lstRetorno.Add(new
						{
							id_sf_despesa = reader["id_sf_despesa"].ToString(),
							id_fornecedor = reader["id_fornecedor"].ToString(),
							cnpj_cpf = reader["cnpj_cpf"].ToString(),
							nome_fornecedor = reader["nome_fornecedor"].ToString(),
							valor = Utils.FormataValor(reader["valor"])
						});
					}

					return lstRetorno;
				}
			}
		}

		internal dynamic GastosMensaisPorAno(int id)
		{
			using (Banco banco = new Banco())
			{
				var strSql = new StringBuilder();
				strSql.AppendLine(@"
					SELECT d.ano, d.mes, SUM(d.valor) AS valor_total
					FROM sf_despesa d
					WHERE d.id_sf_senador = @id
					group by d.ano, d.mes
					order by d.ano, d.mes
				");
				banco.AddParameter("@id", id);

				using (MySqlDataReader reader = banco.ExecuteReader(strSql.ToString()))
				{
					List<dynamic> lstRetorno = new List<dynamic>();
					var lstValoresMensais = new decimal?[12];
					string anoControle = string.Empty;
					bool existeGastoNoAno = false;

					while (reader.Read())
					{
						if (reader["ano"].ToString() != anoControle)
						{
							if (existeGastoNoAno)
							{
								lstRetorno.Add(new
								{
									name = anoControle.ToString(),
									data = lstValoresMensais
								});

								lstValoresMensais = new decimal?[12];
								existeGastoNoAno = false;
							}

							anoControle = reader["ano"].ToString();
						}

						if (Convert.ToDecimal(reader["valor_total"]) > 0)
						{
							lstValoresMensais[Convert.ToInt32(reader["mes"]) - 1] = Convert.ToDecimal(reader["valor_total"]);
							existeGastoNoAno = true;
						}
					}

					if (existeGastoNoAno)
					{
						lstRetorno.Add(new
						{
							name = anoControle.ToString(),
							data = lstValoresMensais
						});
					}

					return lstRetorno;
					// Ex: [{"$id":"1","name":"2015","data":[null,18404.57,25607.82,29331.99,36839.82,24001.68,40811.97,33641.20,57391.30,60477.07,90448.58,13285.14]}]
				}
			}
		}

		internal dynamic Pesquisa()
		{
			using (Banco banco = new Banco())
			{
				var strSql = new StringBuilder();
				strSql.AppendLine(@"
					SELECT 
						id, nome
					FROM sf_senador 
					ORDER BY nome
				");

				var lstRetorno = new List<dynamic>();
				using (MySqlDataReader reader = banco.ExecuteReader(strSql.ToString()))
				{
					while (reader.Read())
					{
						lstRetorno.Add(new
						{
							id = reader["id"].ToString(),
							text = reader["nome"].ToString()
						});
					}
				}
				return lstRetorno;
			}
		}

		internal dynamic Lancamentos(FiltroParlamentarDTO filtro)
		{
			if (filtro == null) throw new BusinessException("Parâmetro filtro não informado!");

			switch (filtro.Agrupamento)
			{
				case eAgrupamentoAuditoria.Parlamentar:
					return LancamentosParlamentar(filtro);
				case eAgrupamentoAuditoria.Despesa:
					return LancamentosDespesa(filtro);
				case eAgrupamentoAuditoria.Fornecedor:
					return LancamentosFornecedor(filtro);
				case eAgrupamentoAuditoria.Partido:
					return LancamentosPartido(filtro);
				case eAgrupamentoAuditoria.Uf:
					return LancamentosEstado(filtro);
				case eAgrupamentoAuditoria.Documento:
					return LancamentosNotaFiscal(filtro);
			}

			throw new BusinessException("Parâmetro filtro.Agrupamento não informado!");
		}

		private dynamic LancamentosParlamentar(FiltroParlamentarDTO filtro)
		{
			using (Banco banco = new Banco())
			{
				var sqlSelect = new StringBuilder();

				sqlSelect.AppendLine(@"
					DROP TABLE IF EXISTS table_in_memory;
					CREATE TEMPORARY TABLE table_in_memory
					SELECT
						count(l.id) AS total_notas
					, sum(l.valor) as valor_total
					, l.id_sf_senador
					FROM sf_despesa l
					WHERE (1=1)
				");

				AdicionaFiltroPeriodo(filtro, sqlSelect);

				AdicionaFiltroSenador(filtro, sqlSelect);

				AdicionaFiltroDespesa(filtro, sqlSelect);

				AdicionaFiltroFornecedor(filtro, sqlSelect);

				AdicionaFiltroPartidoSenador(filtro, sqlSelect);

				AdicionaFiltroEstadoSenador(filtro, sqlSelect);

				sqlSelect.AppendLine(@"
					GROUP BY id_sf_senador;
					
					SELECT
						 d.id as id_sf_senador
						, d.nome as nome_parlamentar
						, e.sigla as sigla_estado
						, p.sigla as sigla_partido
						, l1.total_notas
						, l1.valor_total
						from table_in_memory l1
					LEFT JOIN sf_senador d on d.id = l1.id_sf_senador
					LEFT JOIN partido p on p.id = d.id_partido
					LEFT JOIN estado e on e.id = d.id_estado
				");

				AdicionaResultadoComum(filtro, sqlSelect);

				var lstRetorno = new List<dynamic>();
				using (MySqlDataReader reader = banco.ExecuteReader(sqlSelect.ToString()))
				{
					while (reader.Read())
					{
						lstRetorno.Add(new
						{
							id_sf_senador = reader["id_sf_senador"],
							nome_parlamentar = reader["nome_parlamentar"],
							sigla_estado = reader["sigla_estado"],
							sigla_partido = reader["sigla_partido"],
							total_notas = reader["total_notas"],
							valor_total = Utils.FormataValor(reader["valor_total"])
						});
					}

					reader.NextResult();
					reader.Read();
					string TotalCount = reader[0].ToString();
					string ValorTotal = Utils.FormataValor(reader[1]);

					return new
					{
						total_count = TotalCount,
						valor_total = ValorTotal,
						results = lstRetorno
					};
				}
			}
		}

		private dynamic LancamentosFornecedor(FiltroParlamentarDTO filtro)
		{
			using (Banco banco = new Banco())
			{
				var sqlSelect = new StringBuilder();

				sqlSelect.AppendLine(@"
					DROP TABLE IF EXISTS table_in_memory;
					CREATE TEMPORARY TABLE table_in_memory
					SELECT
						l.id_fornecedor
						, count(l.id) AS total_notas
						, sum(l.valor) as valor_total
					FROM sf_despesa l
					WHERE (1=1)
				");

				AdicionaFiltroPeriodo(filtro, sqlSelect);

				AdicionaFiltroSenador(filtro, sqlSelect);

				AdicionaFiltroDespesa(filtro, sqlSelect);

				AdicionaFiltroFornecedor(filtro, sqlSelect);

				AdicionaFiltroPartidoSenador(filtro, sqlSelect);

				AdicionaFiltroEstadoSenador(filtro, sqlSelect);

				sqlSelect.AppendLine(@"
					GROUP BY l.id_fornecedor;

					select
						l1.id_fornecedor
						, pj.cnpj_cpf
						, pj.nome AS nome_fornecedor
						, l1.total_notas
						, l1.valor_total
					from table_in_memory l1
					LEFT JOIN fornecedor pj on pj.id = l1.id_fornecedor
				");

				AdicionaResultadoComum(filtro, sqlSelect);

				var lstRetorno = new List<dynamic>();
				using (MySqlDataReader reader = banco.ExecuteReader(sqlSelect.ToString()))
				{
					while (reader.Read())
					{
						lstRetorno.Add(new
						{
							//SgUf = reader[SgUfOrdinal],
							//DataUltimaNotaFiscal = Utils.FormataData(reader[DataUltimaNotaFiscalOrdinal]),
							//Doador = reader[DoadorOrdinal],
							id_fornecedor = reader["id_fornecedor"],
							cnpj_cpf = reader["cnpj_cpf"],
							nome_fornecedor = reader["nome_fornecedor"],
							total_notas = reader["total_notas"],
							valor_total = Utils.FormataValor(reader["valor_total"])
						});
					}

					reader.NextResult();
					reader.Read();
					string TotalCount = reader[0].ToString();
					string ValorTotal = Utils.FormataValor(reader[1]);

					return new
					{
						total_count = TotalCount,
						valor_total = ValorTotal,
						results = lstRetorno
					};
				}
			}
		}

		private dynamic LancamentosDespesa(FiltroParlamentarDTO filtro)
		{
			using (Banco banco = new Banco())
			{
				var sqlSelect = new StringBuilder();

				sqlSelect.AppendLine(@"
					DROP TABLE IF EXISTS table_in_memory;
					CREATE TEMPORARY TABLE table_in_memory
					SELECT
						count(l.id) AS total_notas
						, sum(l.valor) as valor_total
						, l.id_sf_despesa_tipo
					FROM sf_despesa l
					WHERE (1=1)
				");

				AdicionaFiltroPeriodo(filtro, sqlSelect);

				AdicionaFiltroSenador(filtro, sqlSelect);

				AdicionaFiltroDespesa(filtro, sqlSelect);

				AdicionaFiltroFornecedor(filtro, sqlSelect);

				AdicionaFiltroPartidoSenador(filtro, sqlSelect);

				AdicionaFiltroEstadoSenador(filtro, sqlSelect);

				sqlSelect.AppendLine(@"
					GROUP BY id_sf_despesa_tipo;
					
					SELECT
						l1.id_sf_despesa_tipo
						, td.descricao
						, l1.total_notas
						, l1.valor_total
					from table_in_memory l1
					LEFT JOIN sf_despesa_tipo td on td.id = l1.id_sf_despesa_tipo
				");

				AdicionaResultadoComum(filtro, sqlSelect);

				var lstRetorno = new List<dynamic>();
				using (MySqlDataReader reader = banco.ExecuteReader(sqlSelect.ToString()))
				{
					while (reader.Read())
					{
						lstRetorno.Add(new
						{
							id_sf_despesa_tipo = reader["id_sf_despesa_tipo"],
							descricao = reader["descricao"],
							total_notas = reader["total_notas"],
							valor_total = Utils.FormataValor(reader["valor_total"])
						});
					}

					reader.NextResult();
					reader.Read();
					string TotalCount = reader[0].ToString();
					string ValorTotal = Utils.FormataValor(reader[1]);

					return new
					{
						total_count = TotalCount,
						valor_total = ValorTotal,
						results = lstRetorno
					};
				}
			}
		}

		private dynamic LancamentosPartido(FiltroParlamentarDTO filtro)
		{
			using (Banco banco = new Banco())
			{
				var sqlSelect = new StringBuilder();

				sqlSelect.AppendLine(@"
					DROP TABLE IF EXISTS table_in_memory;
					CREATE TEMPORARY TABLE table_in_memory
					AS (
						SELECT
						 d.id_partido
						, p.nome as nome_partido
						, count(l1.total_notas) as total_notas
						, sum(l1.valor_total) as valor_total
						from (
							SELECT
							 count(l.id) AS total_notas
							, sum(l.valor) as valor_total
							, l.id_sf_senador
							FROM sf_despesa l
							WHERE (1=1)
				");

				AdicionaFiltroPeriodo(filtro, sqlSelect);

				AdicionaFiltroSenador(filtro, sqlSelect);

				AdicionaFiltroDespesa(filtro, sqlSelect);

				AdicionaFiltroFornecedor(filtro, sqlSelect);

				AdicionaFiltroPartidoSenador(filtro, sqlSelect);

				AdicionaFiltroEstadoSenador(filtro, sqlSelect);

				sqlSelect.AppendLine(@"
						GROUP BY id_sf_senador
					) l1
					INNER JOIN sf_senador d on d.id = l1.id_sf_senador
					LEFT JOIN partido p on p.id = d.id_partido
					GROUP BY p.id, p.nome
				);"); //end table_in_memory

				sqlSelect.AppendLine("select * from table_in_memory ");
				AdicionaResultadoComum(filtro, sqlSelect);

				var lstRetorno = new List<dynamic>();
				using (MySqlDataReader reader = banco.ExecuteReader(sqlSelect.ToString()))
				{
					while (reader.Read())
					{
						lstRetorno.Add(new
						{
							id_partido = reader["id_partido"],
							nome_partido = reader["nome_partido"],
							total_notas = reader["total_notas"],
							valor_total = Utils.FormataValor(reader["valor_total"])
						});
					}

					reader.NextResult();
					reader.Read();
					string TotalCount = reader[0].ToString();
					string ValorTotal = Utils.FormataValor(reader[1]);

					return new
					{
						total_count = TotalCount,
						valor_total = ValorTotal,
						results = lstRetorno
					};
				}
			}
		}

		private dynamic LancamentosEstado(FiltroParlamentarDTO filtro)
		{
			using (Banco banco = new Banco())
			{
				var sqlSelect = new StringBuilder();

				sqlSelect.AppendLine(@"
					DROP TABLE IF EXISTS table_in_memory;
					CREATE TEMPORARY TABLE table_in_memory
					AS (
						SELECT
						 d.id_estado
						, e.nome as nome_estado
						, count(l1.total_notas) as total_notas
						, sum(l1.valor_total) as valor_total
						from (

							SELECT
							 count(l.id) AS total_notas
							, sum(l.valor) as valor_total
							, l.id_sf_senador
							FROM sf_despesa l
							WHERE (1=1)
				");

				AdicionaFiltroPeriodo(filtro, sqlSelect);

				AdicionaFiltroSenador(filtro, sqlSelect);

				AdicionaFiltroDespesa(filtro, sqlSelect);

				AdicionaFiltroFornecedor(filtro, sqlSelect);

				AdicionaFiltroPartidoSenador(filtro, sqlSelect);

				AdicionaFiltroEstadoSenador(filtro, sqlSelect);

				sqlSelect.AppendLine(@"
						GROUP BY id_sf_senador
					) l1
					INNER JOIN sf_senador d on d.id = l1.id_sf_senador
					LEFT JOIN estado e on e.id = d.id_estado
					GROUP BY e.id, e.nome
				); "); //end table_in_memory

				sqlSelect.AppendLine(@"select * from table_in_memory ");
				AdicionaResultadoComum(filtro, sqlSelect);

				var lstRetorno = new List<dynamic>();
				using (MySqlDataReader reader = banco.ExecuteReader(sqlSelect.ToString()))
				{
					while (reader.Read())
					{
						lstRetorno.Add(new
						{
							id_estado = reader["id_estado"],
							nome_estado = reader["nome_estado"],
							total_notas = reader["total_notas"],
							valor_total = Utils.FormataValor(reader["valor_total"])
						});
					}

					reader.NextResult();
					reader.Read();
					string TotalCount = reader[0].ToString();
					string ValorTotal = Utils.FormataValor(reader[1]);

					return new
					{
						total_count = TotalCount,
						valor_total = ValorTotal,
						results = lstRetorno
					};
				}
			}
		}

		private dynamic LancamentosNotaFiscal(FiltroParlamentarDTO filtro)
		{

			//sqlSelect.AppendLine(" p.IdeCadastro as IdCadastro");
			//sqlSelect.AppendLine(", p.nuDeputadoId as IdDeputado");
			//sqlSelect.AppendLine(", l.id as Id");
			//sqlSelect.AppendLine(", l.ideDocumento as IdDocumento");
			//sqlSelect.AppendLine(", l.txtNumero as NotaFiscal");
			//sqlSelect.AppendLine(", l.txtCNPJCPF AS Codigo");
			//sqlSelect.AppendLine(", l.numano as NumAno");
			//sqlSelect.AppendLine(", l.txtNumero as Numero");
			//sqlSelect.AppendLine(", l.datEmissao as DataEmissao");
			//sqlSelect.AppendLine(", SUBSTRING(IFNULL(f.txtbeneficiario, l.txtbeneficiario), 1, 50) AS NomeBeneficiario");
			//sqlSelect.AppendLine(", l.txNomeParlamentar as NomeParlamentar");
			//sqlSelect.AppendLine(", SUM(l.vlrLiquido) AS vlrTotal ");

			using (Banco banco = new Banco())
			{
				var sqlSelect = new StringBuilder();

				//sqlSelect.AppendLine("DROP TABLE IF EXISTS table_in_memory; ");
				//sqlSelect.AppendLine("CREATE TEMPORARY TABLE table_in_memory ");
				//sqlSelect.AppendLine("AS ( ");
				sqlSelect.AppendLine(@"
					SELECT SQL_CALC_FOUND_ROWS
						 l.id as id_sf_despesa
						, l.data_documento
						, l.id_fornecedor
						, pj.cnpj_cpf
						, pj.nome AS nome_fornecedor
						, d.id as id_senador
						, d.nome as nome_parlamentar
						, l.valor as valor_total
					FROM sf_despesa l
					LEFT JOIN sf_senador d on d.id = l.id_sf_senador
					LEFT JOIN fornecedor pj on pj.id = l.id_fornecedor
					WHERE (1=1)
				");

				AdicionaFiltroPeriodo(filtro, sqlSelect);

				AdicionaFiltroSenador(filtro, sqlSelect);

				AdicionaFiltroDespesa(filtro, sqlSelect);

				AdicionaFiltroFornecedor(filtro, sqlSelect);

				AdicionaFiltroPartidoSenador(filtro, sqlSelect);

				AdicionaFiltroEstadoSenador(filtro, sqlSelect);

				sqlSelect.AppendFormat("ORDER BY {0} ", string.IsNullOrEmpty(filtro.sorting) ? "l.data_documento desc, l.valor desc" : Utils.MySqlEscape(filtro.sorting));
				sqlSelect.AppendFormat("LIMIT {0},{1}; ", (filtro.page - 1) * filtro.count, filtro.count);

				sqlSelect.AppendFormat("SELECT FOUND_ROWS();");

				var lstRetorno = new List<dynamic>();
				using (MySqlDataReader reader = banco.ExecuteReader(sqlSelect.ToString()))
				{
					while (reader.Read())
					{
						lstRetorno.Add(new
						{
							id_sf_despesa = reader["id_sf_despesa"],
							data_documento = Utils.FormataData(reader["data_documento"]),
							id_fornecedor = reader["id_fornecedor"],
							cnpj_cpf = reader["cnpj_cpf"],
							nome_fornecedor = reader["nome_fornecedor"].ToString(),
							id_senador = reader["id_senador"],
							nome_parlamentar = reader["nome_parlamentar"].ToString(),
							valor_total = Utils.FormataValor(reader["valor_total"])
						});
					}

					reader.NextResult();
					reader.Read();
					string TotalCount = reader[0].ToString();
					string ValorTotal = null; //Utils.FormataValor(reader[1]);

					return new
					{
						total_count = TotalCount,
						valor_total = ValorTotal,
						results = lstRetorno
					};
				}
			}
		}

		private static void AdicionaFiltroPeriodo(FiltroParlamentarDTO filtro, StringBuilder sqlSelect)
		{
			DateTime dataIni = DateTime.Today;
			DateTime dataFim = DateTime.Today;
			switch (filtro.Periodo)
			{
				case "1": //PERIODO_MES_ATUAL
					sqlSelect.AppendLine(" AND l.ano_mes = " + dataIni.ToString("yyyyMM"));
					break;

				case "2": //PERIODO_MES_ANTERIOR
					dataIni = dataIni.AddMonths(-1);
					sqlSelect.AppendLine(" AND l.ano_mes = " + dataIni.ToString("yyyyMM"));
					break;

				case "3": //PERIODO_MES_ULT_4
					dataIni = dataIni.AddMonths(-3);
					sqlSelect.AppendLine(" AND l.ano_mes >= " + dataIni.ToString("yyyyMM"));
					break;

				case "4": //PERIODO_ANO_ATUAL
					dataIni = new DateTime(dataIni.Year, 1, 1);
					sqlSelect.AppendLine(" AND l.ano_mes >= " + dataIni.ToString("yyyyMM"));
					break;

				case "5": //PERIODO_ANO_ANTERIOR
					dataIni = new DateTime(dataIni.Year, 1, 1).AddYears(-1);
					dataFim = new DateTime(dataIni.Year, 12, 31);
					sqlSelect.AppendFormat(" AND l.ano_mes BETWEEN {0} AND {1}", dataIni.ToString("yyyyMM"), dataFim.ToString("yyyyMM"));
					break;

				case "9": //PERIODO_MANDATO_56
					sqlSelect.AppendLine(" AND l.ano_mes BETWEEN 201502 AND 202301");
					break;

				case "8": //PERIODO_MANDATO_55
					sqlSelect.AppendLine(" AND l.ano_mes BETWEEN 201102 AND 201901");
					break;

				case "7": //PERIODO_MANDATO_54
					sqlSelect.AppendLine(" AND l.ano_mes BETWEEN 200702 AND 201101");
					break;

				case "6": //PERIODO_MANDATO_53
					sqlSelect.AppendLine(" AND l.ano_mes BETWEEN 200702 AND 201001");
					break;
			}
		}

		private static void AdicionaFiltroEstadoSenador(FiltroParlamentarDTO filtro, StringBuilder sqlSelect)
		{
			if (!string.IsNullOrEmpty(filtro.Partido))
			{
				sqlSelect.AppendLine("	AND l.id_sf_senador IN (SELECT id FROM sf_senador where id_partido IN(" + Utils.MySqlEscapeNumberToIn(filtro.Partido) + ")) ");
			}
		}

		private static void AdicionaFiltroPartidoSenador(FiltroParlamentarDTO filtro, StringBuilder sqlSelect)
		{
			if (!string.IsNullOrEmpty(filtro.Uf))
			{
				sqlSelect.AppendLine("	AND l.id_sf_senador IN (SELECT id FROM sf_senador where id_estado IN(" + Utils.MySqlEscapeNumberToIn(filtro.Uf) + ")) ");
			}
		}

		private static void AdicionaFiltroFornecedor(FiltroParlamentarDTO filtro, StringBuilder sqlSelect)
		{
			if (!string.IsNullOrEmpty(filtro.Fornecedor))
			{
				filtro.Fornecedor = String.Join("", System.Text.RegularExpressions.Regex.Split(filtro.Fornecedor, @"[^\d]"));

				if (!string.IsNullOrEmpty(filtro.Fornecedor))
				{
					if (filtro.Fornecedor.Length == 14 || filtro.Fornecedor.Length == 11)
					{
						using (Banco banco = new Banco())
						{
							var id_fornecedor =
								banco.ExecuteScalar("select id from fornecedor where cnpj_cpf = '" + Utils.RemoveCaracteresNaoNumericos(filtro.Fornecedor) + "'");

							if (!Convert.IsDBNull(id_fornecedor))
							{
								sqlSelect.AppendLine("	AND l.id_fornecedor =" + id_fornecedor + " ");
							}
						}
					}
					else
					{
						sqlSelect.AppendLine("	AND l.id_fornecedor =" + Utils.RemoveCaracteresNaoNumericos(filtro.Fornecedor) + " ");
					}
				}
			}
		}

		private static void AdicionaFiltroDespesa(FiltroParlamentarDTO filtro, StringBuilder sqlSelect)
		{
			if (!string.IsNullOrEmpty(filtro.Despesa))
			{
				sqlSelect.AppendLine("	AND l.id_sf_despesa_tipo IN (" + Utils.MySqlEscapeNumberToIn(filtro.Despesa) + ") ");
			}
		}

		private static void AdicionaFiltroSenador(FiltroParlamentarDTO filtro, StringBuilder sqlSelect)
		{
			if (!string.IsNullOrEmpty(filtro.IdParlamentar))
			{
				sqlSelect.AppendLine("	AND l.id_sf_senador IN (" + Utils.MySqlEscapeNumberToIn(filtro.IdParlamentar) + ") ");
			}
		}

		private static void AdicionaResultadoComum(FiltroParlamentarDTO filtro, StringBuilder sqlSelect)
		{
			//sqlSelect.AppendLine("select * from table_in_memory ");
			sqlSelect.AppendFormat("ORDER BY {0} ", string.IsNullOrEmpty(filtro.sorting) ? "valor_total desc" : Utils.MySqlEscape(filtro.sorting));
			sqlSelect.AppendFormat("LIMIT {0},{1}; ", (filtro.page - 1) * filtro.count, filtro.count);

			sqlSelect.AppendLine(
				@"SELECT count(1), sum(valor_total) as valor_total
				FROM table_in_memory; ");
		}

		internal dynamic TipoDespesa()
		{
			using (Banco banco = new Banco())
			{
				var strSql = new StringBuilder();
				strSql.AppendLine("SELECT id, descricao FROM sf_despesa_tipo ");
				strSql.AppendFormat("ORDER BY descricao ");

				var lstRetorno = new List<dynamic>();
				using (MySqlDataReader reader = banco.ExecuteReader(strSql.ToString()))
				{
					while (reader.Read())
					{
						lstRetorno.Add(new
						{
							id = reader["id"].ToString(),
							text = reader["descricao"].ToString(),
						});
					}
				}
				return lstRetorno;
			}
		}
	}
}