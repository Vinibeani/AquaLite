using System;
using System.Globalization;
using System.IO;
using System.Net;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Threading;

namespace AquaLite {
 public sealed class Weather : IDisposable {
  [DataContract] public sealed class Response {[DataMember] public Current current;}
  [DataContract] public sealed class Current {
   [DataMember] public double? temperature_2m;
   [DataMember] public int? weather_code;
  }
  readonly object gate=new object();
  string key="",text;DateTime next=DateTime.MinValue,updated=DateTime.MinValue;
  bool busy,disposed;
  public void Update(Settings settings,Reading reading){
   if(!settings.WeatherEnabled){lock(gate){key="";text=null;}return;}
   double lat=settings.WeatherLatitude,lon=settings.WeatherLongitude;
   if(!SensorReading.IsFinite(lat)||!SensorReading.IsFinite(lon)||lat< -90||lat>90||lon< -180||lon>180)return;
   string requested=lat.ToString("0.####",CultureInfo.InvariantCulture)+","+lon.ToString("0.####",CultureInfo.InvariantCulture);
   lock(gate){
    if(key!=requested){key=requested;text=null;updated=DateTime.MinValue;next=DateTime.MinValue;}
    if(updated>DateTime.UtcNow.AddMinutes(-30))reading.WeatherText=text;
    if(disposed||busy||DateTime.UtcNow<next)return;
    busy=true;next=DateTime.UtcNow.AddMinutes(5);
   }
   ThreadPool.QueueUserWorkItem(delegate{
    string result=null;
    try{
     string[] coordinates=requested.Split(',');
     var request=(HttpWebRequest)WebRequest.Create("https://api.open-meteo.com/v1/forecast?latitude="+coordinates[0]+"&longitude="+coordinates[1]+"&current=temperature_2m,weather_code&timezone=auto&forecast_days=1");
     request.Timeout=7000;request.ReadWriteTimeout=7000;request.UserAgent="AquaLite/0.3";
     using(var response=request.GetResponse())using(var stream=response.GetResponseStream()){
      var data=(Response)new DataContractJsonSerializer(typeof(Response)).ReadObject(stream);
      if(data.current!=null&&data.current.temperature_2m.HasValue&&SensorReading.IsFinite(data.current.temperature_2m.Value))result=Describe(data.current.weather_code)+" • "+data.current.temperature_2m.Value.ToString("0",new CultureInfo("pt-BR"))+" °C";
     }
    }catch(WebException){}catch(IOException){}catch(SerializationException){}catch(InvalidOperationException){}
    finally{lock(gate){busy=false;if(!disposed&&key==requested&&result!=null){text=result;updated=DateTime.UtcNow;next=updated.AddMinutes(15);}}}
   });
  }
  public static string Describe(int? code){
   if(code==0)return "Céu limpo";if(code==1)return "Poucas nuvens";if(code==2)return "Parcialmente nublado";if(code==3)return "Nublado";
   if(code==45||code==48)return "Neblina";if(code>=51&&code<=57)return "Garoa";if(code>=61&&code<=67)return "Chuva";
   if(code>=71&&code<=77)return "Neve";if(code>=80&&code<=82)return "Pancadas de chuva";if(code==85||code==86)return "Pancadas de neve";
   if(code>=95&&code<=99)return "Trovoadas";return "Clima";
  }
  public void Dispose(){lock(gate){disposed=true;key="";text=null;}}
 }
}
