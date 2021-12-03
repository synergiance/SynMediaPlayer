#ifndef SYN_ASPECT_SAMPLE
#define SYN_ASPECT_SAMPLE

float4 sampleAspect(sampler2D tex, float2 uv, float aspectRatio, float nativeRatio) {
	float ratio = aspectRatio / (abs(nativeRatio) > 0.0001 ? nativeRatio : 0.0001);
	float2 aspectMod = abs(ratio) > 1 ? float2(1,ratio) * (ratio > 0 ? 1 : -1) : float2(1/ratio,1);
	uv *= aspectMod;
	float screenMask = 1;
	float2 fw = fwidth(uv);
	uv += (1 - aspectMod) * 0.5;
	screenMask *= smoothstep(-fw.x, fw.x, uv.x);
	screenMask *= smoothstep(-fw.y, fw.y, uv.y);
	screenMask *= smoothstep(1 + fw.x, 1 - fw.x, uv.x);
	screenMask *= smoothstep(1 + fw.y, 1 - fw.y, uv.y);
	// sample the texture
	float4 col = tex2D(tex, uv);
	col.rgb *= screenMask;
	return col;
}

float4 sampleAspectNoSmoothing(sampler2D tex, float2 uv, float aspectRatio, float nativeRatio) {
	float ratio = aspectRatio / (abs(nativeRatio) > 0.0001 ? nativeRatio : 0.0001);
	float2 aspectMod = abs(ratio) > 1 ? float2(1,ratio) * (ratio > 0 ? 1 : -1) : float2(1/ratio,1);
	uv *= aspectMod;
	float screenMask = 1;
	uv += (1 - aspectMod) * 0.5;
	screenMask *= uv.x > 0 ? 1 : 0;
	screenMask *= uv.y > 0 ? 1 : 0;
	screenMask *= uv.x > 1 ? 0 : 1;
	screenMask *= uv.y > 1 ? 0 : 1;
	// sample the texture
	float4 col = tex2D(tex, uv);
	col.rgb *= screenMask;
	return col;
}

#endif