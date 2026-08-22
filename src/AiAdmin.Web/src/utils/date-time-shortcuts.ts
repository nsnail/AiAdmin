export function getDateTimeShortcuts(t: (key: string) => string) {
    const now = new Date()
    const today = new Date(now)
    today.setHours(0, 0, 0, 0)
    const tomorrow = new Date(today)
    tomorrow.setDate(tomorrow.getDate() + 1)
    const currentHour = new Date(now)
    currentHour.setMinutes(0, 0, 0)
    const nextHour = new Date(currentHour)
    nextHour.setHours(nextHour.getHours() + 1)
    const yesterday = new Date(today)
    yesterday.setDate(yesterday.getDate() - 1)
    const weekStart = new Date(today)
    weekStart.setDate(weekStart.getDate() - weekStart.getDay())
    const nextWeek = new Date(weekStart)
    nextWeek.setDate(nextWeek.getDate() + 7)
    const monthStart = new Date(today.getFullYear(), today.getMonth(), 1)
    const nextMonth = new Date(today.getFullYear(), today.getMonth() + 1, 1)
    const range = (start: Date, end: Date) => [start, end]
    return [
        { text: t('table.searchBar.lastHour'), value: () => range(new Date(now.getTime() - 3600000), new Date()) },
        { text: t('table.searchBar.currentHour'), value: () => range(new Date(currentHour), new Date(nextHour)) },
        { text: t('table.searchBar.previousHour'), value: () => range(new Date(now.getTime() - 7200000), new Date(now.getTime() - 3600000)) },
        {
            text: t('table.searchBar.yesterdayAtThisTime'),
            value: () => range(new Date(yesterday.getTime() + (now.getTime() - today.getTime())), new Date()),
        },
        { text: t('table.searchBar.today'), value: () => range(new Date(today), new Date(tomorrow)) },
        { text: t('table.searchBar.yesterday'), value: () => range(new Date(yesterday), new Date(today)) },
        {
            text: t('table.searchBar.previousDay'),
            value: () => {
                const start = new Date(yesterday)
                start.setDate(start.getDate() - 1)
                return range(start, new Date(yesterday))
            },
        },
        { text: t('table.searchBar.thisWeek'), value: () => range(new Date(weekStart), new Date(nextWeek)) },
        {
            text: t('table.searchBar.previousWeek'),
            value: () => {
                const start = new Date(weekStart)
                start.setDate(start.getDate() - 7)
                return range(start, new Date(weekStart))
            },
        },
        { text: t('table.searchBar.thisMonth'), value: () => range(new Date(monthStart), new Date(nextMonth)) },
        {
            text: t('table.searchBar.previousMonth'),
            value: () => {
                const start = new Date(monthStart.getFullYear(), monthStart.getMonth() - 1, 1)
                return range(start, new Date(monthStart))
            },
        },
    ]
}