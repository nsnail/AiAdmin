const tagBackgroundColors = ['#d9ecff', '#e1f3d8', '#faecd8', '#fde2e2', '#e9e9eb', '#eadcf8', '#d9f2ec', '#f8e3d2']
const tagBorderColors = ['#a8d4ff', '#b9e2a5', '#f3d19e', '#f8bebe', '#c8c9cc', '#d2b8ec', '#a8dfd0', '#f0c9a2']
const darkTagBorderColors = ['#477da8', '#5c8f48', '#a17f4b', '#aa6969', '#777a80', '#8665a6', '#4f9682', '#a1744d']
const tagTextColors = ['#337ecc', '#529b2e', '#b88230', '#c45656', '#73767a', '#7a4aa8', '#16866f', '#a35d1d']

export function getHashTagStyle(text: string): { backgroundColor: string; borderColor: string; color: string } {
    let hash = 0
    for (const character of text) hash = (hash * 31 + character.charCodeAt(0)) | 0
    const index = Math.abs(hash) % tagBackgroundColors.length
    const isDarkMode = typeof document !== 'undefined' && document.documentElement.classList.contains('dark')
    return {
        backgroundColor: isDarkMode ? 'var(--el-tag-bg-color)' : tagBackgroundColors[index],
        borderColor: isDarkMode ? darkTagBorderColors[index] : tagBorderColors[index],
        color: tagTextColors[index],
    }
}