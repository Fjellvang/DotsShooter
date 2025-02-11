import{d as F,aj as L,c as g,x as w,M as N,v as I,L as R,a8 as C,a9 as V,b as P,o,e as p,w as a,k as s,p as r,l as f,a as d,t as h,n as _,_ as K}from"./index-B7jvAjdE.js";import{M as l,a as u,c as S,b as T}from"./metaListUtils-B-xQ0HnD.js";import{G as W}from"./GameConfigActionPublish-R7B0H1nr.js";import{g as k}from"./gameConfigs-DlpfRS5r.js";import"./MActionModal-coQLkvWs.js";import"./index-fVjXqxSF.js";import"./MInputSwitch-C-Z-JJsG.js";import"./index-B_QkKjtG.js";const j=1,E=F({__name:"GameConfigPublishedListCard",setup(B,{expose:t}){t();const b=L(),e=g(()=>b.path),{data:c}=w(k()),M=g(()=>{if(c.value!==void 0)return c.value.filter(n=>n.publishedAt!==null||n.unpublishedAt!==null||n.isActive)});function m(n){const y=e.value,G=y.endsWith("/")?"":"/";return y+G+n}function v(n){return n.id}function i(n){return n.isActive?"2525-12-25T00:00:00.0000000Z":n.unpublishedAt??"1970-01-01T00:00:00.0000000Z"}const D=[new l("Time",i,u.Ascending),new l("Time",i,u.Descending),new l("Name","name",u.Ascending),new l("Name","name",u.Descending)],O=["id","name","description"],x=[new T("archived",[new S("Archived",n=>n.isArchived),new S("Not archived",n=>!n.isArchived,!0)])],A={route:b,detailsRoute:e,allGameConfigsDataRaw:c,allGameConfigsData:M,getDetailPagePath:m,getItemKey:v,getPublishedSortKey:i,defaultSortOption:j,sortOptions:D,searchFields:O,filterSets:x,computed:g,get useRoute(){return L},get MetaListFilterOption(){return S},get MetaListFilterSet(){return T},get MetaListSortDirection(){return u},get MetaListSortOption(){return l},get MBadge(){return N},get MListItem(){return I},get MTooltip(){return R},get MTextButton(){return C},get maybePluralString(){return V},get useSubscription(){return w},GameConfigActionPublish:W,get getAllGameConfigsSubscriptionOptions(){return k}};return Object.defineProperty(A,"__isScriptSetup",{enumerable:!1,value:!0}),A}}),Z={key:0},H={key:0},U={key:1},q={key:1},z={key:0},J={key:1},Q={key:0,class:"tw-text-orange-500"};function X(B,t,b,e,c,M){const m=P("meta-time"),v=P("meta-list-card");return o(),p(v,{title:"Publish History",itemList:e.allGameConfigsData,getItemKey:e.getItemKey,searchFields:e.searchFields,filterSets:e.filterSets,sortOptions:e.sortOptions,defaultSortOption:e.defaultSortOption,icon:"table","data-testid":"game-config-published-list-card"},{"item-card":a(({item:i})=>[s(e.MListItem,null,{badge:a(()=>[i.isActive?(o(),p(e.MBadge,{key:0,variant:"success"},{default:a(()=>t[0]||(t[0]=[r("Active")]),void 0,!0),_:1})):f("",!0),i.isArchived?(o(),p(e.MBadge,{key:1,variant:"neutral"},{default:a(()=>t[1]||(t[1]=[r("Archived")]),void 0,!0),_:1})):f("",!0),i.bestEffortStatus==="Failed"?(o(),p(e.MBadge,{key:2,variant:"danger"},{default:a(()=>t[2]||(t[2]=[r("Failed")]),void 0,!0),_:1})):f("",!0)]),"top-right":a(()=>[i.isActive?(o(),d("div",Z,[i.publishedAt!=null?(o(),d("div",H,[t[3]||(t[3]=r("Published ")),s(m,{date:i.publishedAt},null,8,["date"])])):(o(),d("div",U,[s(e.MTooltip,{content:"The date was not recorded as it was published in an earlier version."},{default:a(()=>t[4]||(t[4]=[r("Date unavailable")]),void 0,!0),_:1})]))])):(o(),d("div",q,[i.unpublishedAt!=null?(o(),d("div",z,[t[5]||(t[5]=r("Unpublished ")),s(m,{date:i.unpublishedAt},null,8,["date"])])):(o(),d("div",J,[s(e.MTooltip,{content:"The date was not recorded as it was unpublished in an earlier version."},{default:a(()=>t[6]||(t[6]=[r("Date unavailable")]),void 0,!0),_:1})]))]))]),"bottom-left":a(()=>[i.buildReportSummary?.totalLogLevelCounts.Warning?(o(),d("div",Q,h(e.maybePluralString(i.buildReportSummary.totalLogLevelCounts.Warning,"build warning")),1)):f("",!0),_("div",null,h(i.description||"No description available"),1)]),"bottom-right":a(()=>[_("div",null,[s(e.MTextButton,{to:`gameConfigs/diff?newRoot=${i.id}`,"disabled-tooltip":i.isActive?"The active game config cannot be compared to itself.":void 0,"data-testid":"diff-config"},{default:a(()=>t[7]||(t[7]=[r("Diff to active")]),void 0,!0),_:2},1032,["to","disabled-tooltip"])]),_("div",null,[s(e.MTextButton,{to:e.getDetailPagePath(i.id),"data-testid":"view-config"},{default:a(()=>t[8]||(t[8]=[r("View config")]),void 0,!0),_:2},1032,["to"])]),s(e.GameConfigActionPublish,{gameConfigId:i.id,"text-button":""},null,8,["gameConfigId"])]),default:a(()=>[r(h(i.name||"No name available"),1)],void 0,!0),_:2},1024)]),_:1},8,["itemList"])}const rt=K(E,[["render",X],["__file","GameConfigPublishedListCard.vue"]]);export{rt as default};
