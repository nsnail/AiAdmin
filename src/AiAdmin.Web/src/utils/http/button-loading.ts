const REQUEST_BUTTON_SELECTOR = 'button, [role="button"], [data-api-button], .el-button, .button'
const LOADING_CLASS = 'art-api-loading'

const requestButtons = new WeakMap<object, HTMLElement>()
const pendingRequests = new WeakMap<HTMLElement, number>()
const cleanupTimers = new WeakMap<HTMLElement, ReturnType<typeof setTimeout>>()

let activeButton: HTMLElement | undefined
let settlingButton: HTMLElement | undefined

const findRequestButton = (target: EventTarget | null): HTMLElement | undefined => {
  if (!(target instanceof HTMLElement)) return undefined
  return target.closest<HTMLElement>(REQUEST_BUTTON_SELECTOR) ?? undefined
}

const startLoading = (button: HTMLElement): void => {
  const cleanupTimer = cleanupTimers.get(button)
  if (cleanupTimer) clearTimeout(cleanupTimer)

  pendingRequests.set(button, (pendingRequests.get(button) ?? 0) + 1)
  button.classList.add(LOADING_CLASS)
  button.setAttribute('aria-busy', 'true')
  settlingButton = button
}

const stopLoading = (button: HTMLElement): void => {
  const pending = Math.max((pendingRequests.get(button) ?? 1) - 1, 0)
  pendingRequests.set(button, pending)
  if (pending > 0) return

  const cleanupTimer = setTimeout(() => {
    if ((pendingRequests.get(button) ?? 0) > 0) return
    button.classList.remove(LOADING_CLASS)
    button.removeAttribute('aria-busy')
    if (settlingButton === button) settlingButton = undefined
    cleanupTimers.delete(button)
  }, 0)
  cleanupTimers.set(button, cleanupTimer)
}

if (typeof document !== 'undefined') {
  document.addEventListener(
    'click',
    (event) => {
      const button = findRequestButton(event.target)
      if (!button) return

      if (button.classList.contains(LOADING_CLASS)) {
        event.preventDefault()
        event.stopImmediatePropagation()
        return
      }

      activeButton = button
      setTimeout(() => {
        if (activeButton === button) activeButton = undefined
      }, 0)
    },
    true
  )
}

/**
 * 将本次 HTTP 请求与触发它的按钮关联
 * @param request Axios 请求配置对象
 */
export const bindRequestButton = (request: object): void => {
  const button = activeButton ?? settlingButton
  if (!button || !button.isConnected) return
  requestButtons.set(request, button)
  startLoading(button)
}

/**
 * 请求结束后释放按钮 loading 状态
 * @param request Axios 请求配置对象
 */
export const releaseRequestButton = (request: unknown): void => {
  if (!request || typeof request !== 'object') return
  const button = requestButtons.get(request)
  if (!button) return
  requestButtons.delete(request)
  stopLoading(button)
}
