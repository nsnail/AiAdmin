import { writeFile } from 'node:fs/promises'
import { dirname, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'

;(globalThis as typeof globalThis & { __APP_VERSION__: string }).__APP_VERSION__ = '3.0.2'

const { asyncRoutes } = await import('../src/router/routes/asyncRoutes')
const scriptDir = dirname(fileURLToPath(import.meta.url))
const outputPath = resolve(scriptDir, '../../AiAdmin.Api/Data/menu-seed.json')

await writeFile(outputPath, `${JSON.stringify(asyncRoutes, null, 2)}\n`, 'utf8')
console.log(`Menu seed exported to ${outputPath}`)