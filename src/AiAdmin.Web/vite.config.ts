import { defineConfig, loadEnv, type Plugin } from 'vite'
import vue from '@vitejs/plugin-vue'
import path from 'path'
import { fileURLToPath } from 'url'
import viteCompression from 'vite-plugin-compression'
import Components from 'unplugin-vue-components/vite'
import AutoImport from 'unplugin-auto-import/vite'
import ElementPlus from 'unplugin-element-plus/vite'
import { ElementPlusResolver } from 'unplugin-vue-components/resolvers'
import tailwindcss from '@tailwindcss/vite'
import { readFileSync, readdirSync, realpathSync, statSync, writeFileSync } from 'fs'

/**
 * 递归统计目录下所有文件的字节数之和
 * @param dir 目录路径
 * @returns 字节数
 */
function directorySize(dir: string): number {
    let total = 0
    for (const entry of readdirSync(dir, { withFileTypes: true })) {
        const full = path.join(dir, entry.name)
        if (entry.isDirectory()) total += directorySize(full)
        else if (entry.isFile()) total += statSync(full).size
    }
    return total
}

/**
 * 统计已安装 npm 包的磁盘占用，cnpm 安装的包是符号链接，需要先解析到真实目录
 * @param name 包名称
 * @returns 字节数，未安装或统计失败时为 0
 */
function installedSize(name: string): number {
    try {
        return directorySize(realpathSync(path.resolve(process.cwd(), 'node_modules', name)))
    } catch {
        return 0
    }
}

/**
 * 读取 package.json 中的依赖，并用 node_modules 中实际安装的版本覆盖声明版本（声明为 next、latest 等标签时才有意义）
 * @param group 依赖分组：dependencies 为运行依赖，devDependencies 为开发依赖
 * @returns 依赖名称、声明版本、已安装版本和磁盘占用字节数列表，按名称升序
 */
function readPackageGroup(group: 'dependencies' | 'devDependencies') {
    const pkg = JSON.parse(readFileSync(path.resolve(process.cwd(), 'package.json'), 'utf-8'))
    return Object.entries<string>(pkg[group] ?? {})
        .map(([name, declared]) => {
            let installed = ''
            try {
                installed = JSON.parse(readFileSync(path.resolve(process.cwd(), 'node_modules', name, 'package.json'), 'utf-8')).version ?? ''
            } catch {
                // 依赖未安装时只展示声明版本
            }
            return { name, declared, installed, sizeBytes: installedSize(name) }
        })
        .sort((a, b) => a.name.localeCompare(b.name))
}

/**
 * 前端发布版本，取自环境变量 APP_VERSION（流水线传入 tag 名），去掉 v 前缀；本地构建没有该变量时为空
 * @returns 版本号，例如 1.8.1，没有时为空字符串
 */
function readAppVersion(): string {
    return (process.env.APP_VERSION ?? '').trim().replace(/^v/i, '')
}

/**
 * 构建结束后在产物根目录写入 version.json，供浏览器判断前端是否已经发布到最新版本
 * @param version 前端发布版本
 * @returns Vite 插件
 */
function versionFilePlugin(version: string): Plugin {
    return {
        name: 'emit-version-file',
        apply: 'build',
        closeBundle() {
            if (version) writeFileSync(path.resolve(process.cwd(), 'dist', 'version.json'), JSON.stringify({ version }))
        },
    }
}

export default ({ mode }: { mode: string }) => {
    const root = process.cwd()
    const env = loadEnv(mode, root)
    const { VITE_PORT, VITE_BASE_URL, VITE_API_URL, VITE_API_PROXY_URL } = env

    console.log(`🚀 API_URL = ${VITE_API_URL}`)

    return defineConfig({
        base: VITE_BASE_URL,
        define: {
            __NPM_PACKAGES__: JSON.stringify({
                dependencies: readPackageGroup('dependencies'),
                devDependencies: readPackageGroup('devDependencies'),
            }),
        },
        server: {
            port: Number(VITE_PORT),
            proxy: {
                '/api': {
                    target: VITE_API_PROXY_URL,
                    changeOrigin: true,
                    ws: true,
                },
            },
            host: true,
        },
        // 路径别名
        resolve: {
            alias: {
                '@': fileURLToPath(new URL('./src', import.meta.url)),
                '@views': resolvePath('src/views'),
                '@imgs': resolvePath('src/assets/images'),
                '@icons': resolvePath('src/assets/icons'),
                '@utils': resolvePath('src/utils'),
                '@stores': resolvePath('src/store'),
                '@styles': resolvePath('src/assets/styles'),
            },
        },
        build: {
            target: 'es2015',
            outDir: 'dist',
            chunkSizeWarningLimit: 2000,
            minify: 'terser',
            terserOptions: {
                compress: {
                    // 生产环境去除 console
                    drop_console: true,
                    // 生产环境去除 debugger
                    drop_debugger: true,
                },
            },
            dynamicImportVarsOptions: {
                warnOnError: true,
                exclude: [],
                include: ['src/views/**/*.vue'],
            },
        },
        plugins: [
            vue(),
            versionFilePlugin(readAppVersion()),
            tailwindcss(),
            // 自动按需导入 API
            AutoImport({
                imports: ['vue', 'vue-router', 'pinia', '@vueuse/core'],
                dts: 'src/types/import/auto-imports.d.ts',
                resolvers: [ElementPlusResolver()],
                eslintrc: {
                    enabled: true,
                    filepath: './.auto-import.json',
                    globalsPropValue: true,
                },
            }),
            // 自动按需导入组件
            Components({
                // 只自动注册通用核心组件，业务组件一律显式 import，避免污染全局组件声明
                dirs: ['src/components/core'],
                dts: 'src/types/import/components.d.ts',
                resolvers: [ElementPlusResolver()],
            }),
            // 按需定制主题配置
            ElementPlus({
                useSource: true,
            }),
            // 压缩
            viteCompression({
                verbose: false, // 是否在控制台输出压缩结果
                disable: false, // 是否禁用
                algorithm: 'gzip', // 压缩算法
                ext: '.gz', // 压缩后的文件名后缀
                threshold: 10240, // 只有大小大于该值的资源会被处理 10240B = 10KB
                deleteOriginFile: false, // 压缩后是否删除原文件
            }),
        ],
        // 依赖预构建：避免运行时重复请求与转换，提升首次加载速度
        optimizeDeps: {
            include: [
                'echarts/core',
                'echarts/charts',
                'echarts/components',
                'echarts/renderers',
                'xlsx',
                'crypto-js',
                'file-saver',
                'element-plus/es',
                'element-plus/es/components/*/style/css',
                'element-plus/es/components/*/style/index',
            ],
        },
        css: {
            preprocessorOptions: {
                // sass variable and mixin
                scss: {
                    additionalData: `
            @use "@styles/core/el-light.scss" as *;
            @use "@styles/core/mixin.scss" as *;
          `,
                },
            },
            postcss: {
                plugins: [
                    {
                        postcssPlugin: 'internal:charset-removal',
                        AtRule: {
                            charset: (atRule) => {
                                if (atRule.name === 'charset') {
                                    atRule.remove()
                                }
                            },
                        },
                    },
                ],
            },
        },
    })
}

function resolvePath(paths: string) {
    return path.resolve(__dirname, paths)
}