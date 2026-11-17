/// <reference types="vite/client" />

/** 构建期由 vite.config.ts 注入的前端 npm 依赖清单 */
declare const __NPM_PACKAGES__: {
    dependencies: { name: string; declared: string; installed: string; sizeBytes: number }[]
    devDependencies: { name: string; declared: string; installed: string; sizeBytes: number }[]
}

declare module 'nprogress'

declare module 'crypto-js'

declare module 'file-saver'

declare module 'qrcode.vue' {
    export type Level = 'L' | 'M' | 'Q' | 'H'
    export type RenderAs = 'canvas' | 'svg'
    export type GradientType = 'linear' | 'radial'
    export interface ImageSettings {
        src: string
        height: number
        width: number
        excavate: boolean
    }
    export interface QRCodeProps {
        value: string
        size?: number
        level?: Level
        background?: string
        foreground?: string
        renderAs?: RenderAs
    }
    const QrcodeVue: any
    export default QrcodeVue
}