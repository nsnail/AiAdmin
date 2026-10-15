/**
 * 业务系统接口（账号、设备、任务）
 *
 * 与框架的 system-manage.ts 分文件维护，业务模块新增接口和类型时只修改本文件，
 * 减少与框架代码合并时的冲突和审查范围
 */
import { stringKeyEnabledStateResources, type DynamicTableQuery } from '@/api/system-manage'
import request from '@/utils/http'

// 向框架的资源类型追加业务资源：账号和设备使用字符串主键，任务只参与列表筛选
declare module '@/api/system-manage' {}