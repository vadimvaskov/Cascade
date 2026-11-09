// Deterministic PCM WAV spectral onset extractor. No external dependencies.
#include <algorithm>
#include <cmath>
#include <complex>
#include <cstdint>
#include <fstream>
#include <iostream>
#include <vector>
#include <string>
static void fft(std::vector<std::complex<double>>& a) {
 const int n=a.size();
 for(int i=1,j=0;i<n;i++){int b=n>>1;for(;j&b;b>>=1)j^=b;j^=b;if(i<j)std::swap(a[i],a[j]);}
 const double pi=std::acos(-1.0);
 for(int len=2;len<=n;len<<=1){std::complex<double>wlen(std::cos(-2*pi/len),std::sin(-2*pi/len));for(int i=0;i<n;i+=len){std::complex<double>w(1);for(int j=0;j<len/2;j++){auto u=a[i+j],v=a[i+j+len/2]*w;a[i+j]=u+v;a[i+j+len/2]=u-v;w*=wlen;}}}
}
uint32_t u32(std::istream& f){uint32_t x=0;f.read(reinterpret_cast<char*>(&x),4);return x;}
int main(int argc,char**argv){
 if(argc!=3){std::cerr<<"usage: onsets input.wav output.csv\n";return 1;}
 std::ifstream f(argv[1],std::ios::binary);char id[4];f.read(id,4);if(std::string(id,4)!="RIFF")return 2;u32(f);f.read(id,4);uint16_t channels=0,bits=0,format=0;uint32_t rate=0;std::vector<int16_t> pcm;
 while(f.read(id,4)){auto size=u32(f);auto next=f.tellg()+std::streamoff(size+(size&1));std::string chunk(id,4);if(chunk=="fmt "){f.read(reinterpret_cast<char*>(&format),2);f.read(reinterpret_cast<char*>(&channels),2);rate=u32(f);u32(f);uint16_t block;f.read(reinterpret_cast<char*>(&block),2);f.read(reinterpret_cast<char*>(&bits),2);}else if(chunk=="data"){if(format!=1||bits!=16){std::cerr<<"need PCM16\n";return 3;}pcm.resize(size/2);f.read(reinterpret_cast<char*>(pcm.data()),size);}f.seekg(next);}
 if(rate!=48000||channels<1)return 4;
 const int dec=4,sr=rate/dec,N=1024,H=120;std::vector<double>x(pcm.size()/channels/dec);for(size_t i=0;i<x.size();i++){double y=0;for(int d=0;d<dec;d++)for(int c=0;c<channels;c++)y+=pcm[(i*dec+d)*channels+c]/32768.0;x[i]=y/(dec*channels);}pcm.clear();pcm.shrink_to_fit();
 std::vector<std::complex<double>>a(N);std::vector<double>last(N/2+1),avg(N/2+1,0);std::ofstream out(argv[2]);out<<"time,low_flux,mid_flux,high_flux,rms,low_energy\n";
 for(size_t start=0;start+N<x.size();start+=H){double power=0;for(int i=0;i<N;i++){double v=x[start+i];power+=v*v;a[i]=v*(.5-.5*std::cos(2*std::acos(-1.0)*i/(N-1)));}fft(a);double flux[3]={0,0,0},low=0;
 for(int k=2;k<=N/2;k++){double hz=double(k)*sr/N,mag=std::abs(a[k]);avg[k]=.99*avg[k]+.01*mag;double novelty=std::max(0.0,mag-last[k])/std::max(.2,avg[k]);novelty=std::min(novelty,10.0);int band=hz<180?0:hz<1400?1:2;flux[band]+=novelty;if(hz<180)low+=mag*mag;last[k]=mag;}
 out<<double(start+N/2)/sr<<","<<flux[0]<<","<<flux[1]<<","<<flux[2]<<","<<std::sqrt(power/N)<<","<<std::sqrt(low)<<"\n";
 }std::cerr<<"frames="<<x.size()/H<<" duration="<<double(x.size())/sr<<"\n";
}
