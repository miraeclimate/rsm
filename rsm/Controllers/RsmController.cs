using ESIL.DataWorker;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace rsm.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class RsmController : ControllerBase
    {
        private static readonly string[] Summaries = new[]
        {
            "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
        };

        private readonly ILogger<RsmController> _logger;

        public RsmController(ILogger<RsmController> logger)
        {
            _logger = logger;
        }

        public IEnumerable<WeatherForecast> oldSample()
        {
            var rng = new Random();
            return Enumerable.Range(1, 5).Select(index => new WeatherForecast
            {
                Date = DateTime.Now.AddDays(index),
                TemperatureC = rng.Next(-20, 55),
                Summary = Summaries[rng.Next(Summaries.Length)]
            })
            .ToArray();
        }

        void LogDicArray(string varName, Dictionary<int, double[]> v)
        {
            for (int i=0; i<v.Count; i++)
            {
                _logger.LogInformation("{vName}: key: {k}, values: count={cnt} || {vals}", varName, v.ElementAt(i).Key, v.ElementAt(i).Value.Length, v.ElementAt(i).Value);
            }
        }

        /*
try 
{
    // rsmWorker.rsmApp = rsmWorker.InitApp(rsmPath);
    RSMBaseClass rSMFromFile = new ESILFileWorker().GetRSMFromFile(rsmPath);
    // RSMBaseClass rSMBaseClass = GetRSMFromFile(rsmPath);
    _logger.LogInformation("fitting algo:{algo}", rSMFromFile.predicParams.fittingAlgorithm);
    SFRSMClass sfRsmClass = (SFRSMClass)rSMFromFile;
    _logger.LogInformation("Emission: {em}", sfRsmClass.sfParam.Emssion);
    _logger.LogInformation("Values: {val}", sfRsmClass.sfParam.Values);
    _logger.LogInformation("predic basecase: {bcLen}, {bc}", sfRsmClass.predicParams.BaseCase.Length, sfRsmClass.predicParams.BaseCase);
    LogDicArray("Emission", sfRsmClass.sfParam.Emssion);
    LogDicArray("Values", sfRsmClass.sfParam.Values);
} catch (Exception e)
{
    _logger.LogError("Cannot init rsm: {e}", e);
}*/


        [HttpPost]
        public RsmResponse Post(List<double> inputFactors)
        {
            // string rsmPath = @"C:\rsm\RSM.rsm";
            // string rsmPath = @"C:\rsm\guidePM25.rsm";
            string rsmPath = @"/rsmfiles/PM25_Jan_small_0714_nonlinear.rsm";

            // rsm 파일 읽기(.rsm)
            ESIL.DataWorker.RSMBaseClass rsm = new ESIL.DataWorker.RSMBaseClass();
            ESIL.Kriging.RSMWorker rsmWorker = new ESIL.Kriging.RSMWorker();
            
            rsm = rsmWorker.GetRSMBaseClass(rsmPath);
            
            if (rsm == null)
            {
                _logger.LogInformation("rsm is null !!!");
                return new RsmResponse();
            }

            // RSM Control Factor 산정
            // RSM 섹터(POW, RES, IND, SLV, MOB, OTH, AGR)와 CAPSS SCC코드 매핑 데이터와 시도 배출량 데이터를 이용하여 산정
            // (건대에서 적용한 산정식 적용)
            _logger.LogInformation("rsmParam: {param}", rsm.rsmParam.ToString());
            _logger.LogInformation("rsmParam.Factors count: {param}", rsm.rsmParam.Factors.Count);

            // RSM Control Factor 적용
            ESIL.DataWorker.FactorInfo[] futureFactor = new ESIL.DataWorker.FactorInfo[rsm.rsmParam.Factors.Count];
            _logger.LogInformation("futureFactor count: {count}", futureFactor.Length);
            
            List<double> factors = new List<double>();

            for (int i = 0; i < futureFactor.Length; i++)
            {
                // factor 산정 결과에서 시도와 섹터에 맞는 데이터를 찾는다.
                // futureFactor[i].Region : A,B,C 등 지역
                // futureFactor[i].Source : POW, IND 등의 섹터
                // 찾은 factor 값을 Limit에 넣는다.
                // 찾은 factor 값이 0.5보다 작으면 0.5로, 1.5보다 크면 1.5로 설정
                // futureFactor[i].Limit = 1; // 시도와 섹터가 일치하는 factor값;

                // factors.Add(futureFactor[i].Limit);
                factors.Add(1);
            }
            
            _logger.LogInformation("factors: {factors}", factors);

            _logger.LogInformation("Input factors: {factors}", inputFactors);

            // 팩터 적용하여 셀농도 수정
            double[] val = rsmWorker.GetResponseValue(rsmPath, inputFactors.ToArray());
            _logger.LogInformation("val count: {count}", val.Length);
            // 1차원 배열을 2차원 배열로 수정 : RSM 셀[col, row] 형식으로
            ESIL.DataWorker.ModelAttribute modelAtt = rsmWorker.GetModelAttribute(rsmPath);
            // double[,] valColRow = ESIL.Kriging.DimTransform.SingleToMulti(val, (int)modelAtt.ColCount, (int)modelAtt.RowCount);
            RsmResponse response = new RsmResponse();
            response.values = val;
            response.col = (int)modelAtt.ColCount;
            response.row = (int)modelAtt.RowCount;
            return response;
        }
        public class RsmResponse
        {
            public double[] values { get; set; }
            public int col { get; set; }
            public int row { get; set; }

            public RsmResponse()
            {
                values = new double[0];
                col = 0;
                row = 0;
            }
        }

        public RSMBaseClass GetRSMFromFile(string strPath)
        {
            FileStream fileStream = new FileStream(strPath, FileMode.Open);
            try
            {
                RSMBaseClass rsmBase = Serializer.Deserialize<RSMBaseClass>(fileStream);
                fileStream.Close();
                fileStream.Dispose();
                return rsmBase;
            }
            finally
            {
                if (fileStream != null)
                {
                    ((IDisposable)fileStream).Dispose();
                }
            }
        }

    }
}
