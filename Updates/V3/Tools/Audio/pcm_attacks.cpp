// Independent time-domain attack detector for 48kHz PCM16 WAVs.
// Emits 10ms broadband, low-pass and high-pass RMS, with causal filter state.
#include <cmath>
#include <cstdint>
#include <fstream>
#include <iostream>
#include <string>
#include <vector>
uint32_t read32(std::istream& f){uint32_t n=0;f.read((char*)&n,4);return n;}
struct Filter {
 double b0,b1,b2,a1,a2,x1=0,x2=0,y1=0,y2=0;
 Filter(double hz,bool high){double w=2*std::acos(-1.)*hz/48000,c=std::cos(w),alpha=std::sin(w)/std::sqrt(2.),a0=1+alpha;
 b0=(high?1+c:1-c)/2/a0;b1=(high?-(1+c):1-c)/a0;b2=b0;a1=-2*c/a0;a2=(1-alpha)/a0;}
 double apply(double x){double y=b0*x+b1*x1+b2*x2-a1*y1-a2*y2;x2=x1;x1=x;y2=y1;y1=y;return y;}
};
int main(int argc,char** argv){
 if(argc!=3)return 1;std::ifstream f(argv[1],std::ios::binary);char id[4];f.read(id,4);if(std::string(id,4)!="RIFF")return 2;read32(f);f.read(id,4);
 uint16_t format=0,channels=0,bits=0;uint32_t rate=0;std::vector<int16_t> pcm;
 while(f.read(id,4)){auto size=read32(f);auto next=f.tellg()+std::streamoff(size+(size&1));std::string chunk(id,4);
 if(chunk=="fmt "){f.read((char*)&format,2);f.read((char*)&channels,2);rate=read32(f);read32(f);uint16_t align;f.read((char*)&align,2);f.read((char*)&bits,2);}
 else if(chunk=="data"){if(format!=1||bits!=16)return 3;pcm.resize(size/2);f.read((char*)pcm.data(),size);}f.seekg(next);}
 if(rate!=48000||channels<1||pcm.empty())return 4;
 Filter low(180,false),high(1500,true);std::ofstream out(argv[2]);out<<"time,broad,low,high\n";
 double power[3]={};int count=0;
 for(size_t i=0;i<pcm.size()/channels;i++){double x=0;for(int c=0;c<channels;c++)x+=pcm[i*channels+c]/32768.;x/=channels;
 double l=low.apply(x),h=high.apply(x);power[0]+=x*x;power[1]+=l*l;power[2]+=h*h;
 if(++count==480){out<<(i+1-240)/48000.;for(double& p:power){out<<","<<std::sqrt(p/480);p=0;}out<<"\n";count=0;}}
}
