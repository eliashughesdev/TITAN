
#define WIN32_LEAN_AND_MEAN
#define NOMINMAX
#include <windows.h>
#include <d3d11.h>
#include <dxgi1_2.h>
#include <wrl/client.h>
#include <mutex>
#include <cstdint>
#include <cstring>
#include <new>

#pragma comment(lib, "d3d11.lib")
#pragma comment(lib, "dxgi.lib")
#pragma comment(lib, "user32.lib")

using Microsoft::WRL::ComPtr;

namespace
{
    // Contrato: 0=frame, 1=sin cambios/timeout,
    // 2=reiniciar duplicación, negativos=error.
    constexpr int CAPTURE_OK = 0;
    constexpr int CAPTURE_IDLE = 1;
    constexpr int CAPTURE_RESET = 2;
    constexpr int CAPTURE_INVALID = -1;
    constexpr int CAPTURE_FAILED = -2;
    constexpr int CAPTURE_SMALL_BUFFER = -3;

    struct Context
    {
        std::mutex mutex;
        ComPtr<ID3D11Device> device;
        ComPtr<ID3D11DeviceContext> immediate;
        ComPtr<IDXGIOutputDuplication> duplication;
        ComPtr<ID3D11Texture2D> staging;

        unsigned width = 0;
        unsigned height = 0;
        bool frameOwned = false;
    };

    void ReleaseFrame(Context& ctx)
    {
        if (ctx.frameOwned && ctx.duplication)
        {
            ctx.duplication->ReleaseFrame();
            ctx.frameOwned = false;
        }
    }

    bool IsPrimary(const DXGI_OUTPUT_DESC& desc)
    {
        MONITORINFO info = {};
        info.cbSize = sizeof(info);

        return desc.AttachedToDesktop &&
            GetMonitorInfoW(desc.Monitor, &info) &&
            ((info.dwFlags & MONITORINFOF_PRIMARY) != 0);
    }

    bool Initialize(Context& ctx)
    {
        ComPtr<IDXGIFactory1> factory;
        if (FAILED(CreateDXGIFactory1(
            IID_PPV_ARGS(factory.GetAddressOf()))))
            return false;

        for (UINT adapterIndex = 0; ; ++adapterIndex)
        {
            ComPtr<IDXGIAdapter1> adapter;
            HRESULT hr = factory->EnumAdapters1(
                adapterIndex, adapter.GetAddressOf());

            if (hr == DXGI_ERROR_NOT_FOUND)
                break;
            if (FAILED(hr))
                continue;

            for (UINT outputIndex = 0; ; ++outputIndex)
            {
                ComPtr<IDXGIOutput> output;
                hr = adapter->EnumOutputs(
                    outputIndex, output.GetAddressOf());

                if (hr == DXGI_ERROR_NOT_FOUND)
                    break;
                if (FAILED(hr))
                    continue;

                DXGI_OUTPUT_DESC desc = {};
                if (FAILED(output->GetDesc(&desc)) ||
                    !IsPrimary(desc))
                    continue;

                D3D_FEATURE_LEVEL featureLevel = {};
                const D3D_FEATURE_LEVEL levels[] = {
                    D3D_FEATURE_LEVEL_11_1,
                    D3D_FEATURE_LEVEL_11_0,
                    D3D_FEATURE_LEVEL_10_1,
                    D3D_FEATURE_LEVEL_10_0
                };

                // El dispositivo debe usar el mismo adaptador
                // al que pertenece la salida duplicada.
                hr = D3D11CreateDevice(
                    adapter.Get(),
                    D3D_DRIVER_TYPE_UNKNOWN,
                    nullptr,
                    D3D11_CREATE_DEVICE_BGRA_SUPPORT,
                    levels,
                    ARRAYSIZE(levels),
                    D3D11_SDK_VERSION,
                    ctx.device.GetAddressOf(),
                    &featureLevel,
                    ctx.immediate.GetAddressOf());

                if (FAILED(hr))
                {
                    ctx.device.Reset();
                    ctx.immediate.Reset();
                    continue;
                }

                ComPtr<IDXGIOutput1> output1;
                if (FAILED(output.As(&output1)))
                    continue;

                hr = output1->DuplicateOutput(
                    ctx.device.Get(),
                    ctx.duplication.GetAddressOf());

                if (FAILED(hr))
                {
                    ctx.device.Reset();
                    ctx.immediate.Reset();
                    continue;
                }

                DXGI_OUTDUPL_DESC duplicationDesc = {};
                ctx.duplication->GetDesc(&duplicationDesc);

                ctx.width =
                    duplicationDesc.ModeDesc.Width;
                ctx.height =
                    duplicationDesc.ModeDesc.Height;

                if (!ctx.width || !ctx.height)
                    return false;

                D3D11_TEXTURE2D_DESC stagingDesc = {};
                stagingDesc.Width = ctx.width;
                stagingDesc.Height = ctx.height;
                stagingDesc.MipLevels = 1;
                stagingDesc.ArraySize = 1;
                stagingDesc.Format =
                    duplicationDesc.ModeDesc.Format;
                stagingDesc.SampleDesc.Count = 1;
                stagingDesc.Usage = D3D11_USAGE_STAGING;
                stagingDesc.CPUAccessFlags =
                    D3D11_CPU_ACCESS_READ;

                // Este contrato entrega BGRA8; las otras
                // modalidades requieren conversión explícita.
                if (stagingDesc.Format !=
                    DXGI_FORMAT_B8G8R8A8_UNORM)
                    return false;

                hr = ctx.device->CreateTexture2D(
                    &stagingDesc,
                    nullptr,
                    ctx.staging.GetAddressOf());

                return SUCCEEDED(hr);
            }
        }

        return false;
    }
}

extern "C"
{
    __declspec(dllexport) void* __cdecl
        TitanDxgiOpen()
    {
        Context* ctx = new (std::nothrow) Context();
        if (!ctx)
            return nullptr;

        if (!Initialize(*ctx))
        {
            delete ctx;
            return nullptr;
        }

        return ctx;
    }

    __declspec(dllexport) int __cdecl
        TitanDxgiGetSize(
            void* handle,
            int* width,
            int* height)
    {
        if (!handle || !width || !height)
            return CAPTURE_INVALID;

        Context& ctx = *static_cast<Context*>(handle);
        std::lock_guard<std::mutex> guard(ctx.mutex);

        *width = static_cast<int>(ctx.width);
        *height = static_cast<int>(ctx.height);

        return CAPTURE_OK;
    }

    __declspec(dllexport) int __cdecl
        TitanDxgiGrab(
            void* handle,
            unsigned char* destination,
            int capacity,
            unsigned timeoutMs)
    {
        if (!handle || !destination || capacity <= 0)
            return CAPTURE_INVALID;

        Context& ctx = *static_cast<Context*>(handle);
        std::lock_guard<std::mutex> guard(ctx.mutex);

        if (!ctx.duplication ||
            !ctx.immediate ||
            !ctx.staging)
            return CAPTURE_INVALID;

        const uint64_t required =
            uint64_t(ctx.width) * ctx.height * 4;

        if (required > static_cast<uint64_t>(capacity))
            return CAPTURE_SMALL_BUFFER;

        DXGI_OUTDUPL_FRAME_INFO info = {};
        ComPtr<IDXGIResource> resource;

        HRESULT hr = ctx.duplication->AcquireNextFrame(
            timeoutMs,
            &info,
            resource.GetAddressOf());

        if (hr == DXGI_ERROR_WAIT_TIMEOUT)
            return CAPTURE_IDLE;

        if (hr == DXGI_ERROR_ACCESS_LOST ||
            hr == DXGI_ERROR_DEVICE_REMOVED ||
            hr == DXGI_ERROR_DEVICE_RESET)
            return CAPTURE_RESET;

        if (FAILED(hr))
            return CAPTURE_FAILED;

        ctx.frameOwned = true;

        ComPtr<ID3D11Texture2D> frameTexture;
        hr = resource.As(&frameTexture);

        if (FAILED(hr))
        {
            ReleaseFrame(ctx);
            return CAPTURE_FAILED;
        }

        ctx.immediate->CopyResource(
            ctx.staging.Get(),
            frameTexture.Get());

        D3D11_MAPPED_SUBRESOURCE mapped = {};
        hr = ctx.immediate->Map(
            ctx.staging.Get(),
            0,
            D3D11_MAP_READ,
            0,
            &mapped);

        if (FAILED(hr))
        {
            ReleaseFrame(ctx);
            return CAPTURE_FAILED;
        }

        const size_t rowBytes =
            size_t(ctx.width) * 4;

        auto source =
            static_cast<const unsigned char*>(mapped.pData);

        for (unsigned row = 0; row < ctx.height; ++row)
        {
            std::memcpy(
                destination + size_t(row) * rowBytes,
                source + size_t(row) * mapped.RowPitch,
                rowBytes);
        }

        ctx.immediate->Unmap(ctx.staging.Get(), 0);
        ReleaseFrame(ctx);

        return CAPTURE_OK;
    }

    __declspec(dllexport) void __cdecl
        TitanDxgiClose(void* handle)
    {
        if (!handle)
            return;

        Context* ctx = static_cast<Context*>(handle);

        {
            std::lock_guard<std::mutex> guard(ctx->mutex);
            ReleaseFrame(*ctx);
        }

        delete ctx;
    }
}
