import{d as x,ad as v,L as S,v as w,b as c,o as l,e as d,w as t,n as I,t as s,p as n,k as f,l as g,_ as M}from"./index-B7jvAjdE.js";const b=x({__name:"MetaInputPlayerSelect",props:{value:{},ignorePlayerIds:{}},emits:["input"],setup(p,{expose:a}){a();const i=p,r=v();async function y(u){return(await r.get(`/players/?query=${encodeURIComponent(u??"")}`)).data.map(o=>({id:o.id,value:o,disabled:i.ignorePlayerIds?i.ignorePlayerIds.includes(o.id):!1}))}const m={props:i,gameServerApi:r,search:y,get useGameServerApi(){return v},get MTooltip(){return S},get MListItem(){return w}};return Object.defineProperty(m,"__isScriptSetup",{enumerable:!1,value:!0}),m}}),k={class:"tw-flex"};function P(p,a,i,r,y,m){const u=c("fa-icon"),_=c("meta-time"),o=c("meta-input-select");return l(),d(o,{value:i.value,placeholder:"Search for a player...",options:r.search,"no-clear":"",onInput:a[0]||(a[0]=e=>p.$emit("input",e)),"data-testid":"input-player-select"},{selectedOption:t(({option:e})=>[I("div",null,s(e?.name),1)]),option:t(({option:e})=>[e?.isInitialized?e?.deserializedSuccessfully?e?.deletionStatus.startsWith("Deleted")?(l(),d(r.MListItem,{key:2,class:"text-danger !tw-px-0 !tw-py-0"},{"top-right":t(()=>[n(s(e.id),1)]),"bottom-left":t(()=>a[4]||(a[4]=[n("Player deleted")])),default:t(()=>[n("☠️ "+s(e.name),1)],void 0,!0),_:2},1024)):(l(),d(r.MListItem,{key:3,class:"!tw-px-0 !tw-py-0"},{badge:t(()=>[I("div",k,[e?.totalIapSpend>0?(l(),d(r.MTooltip,{key:0,content:"Total IAP spend: $"+e.totalIapSpend.toFixed(2),noUnderline:""},{default:t(()=>[f(u,{class:"text-muted",icon:"money-check-alt",size:"sm"})],void 0,!0),_:2},1032,["content"])):g("",!0),e?.isDeveloper?(l(),d(r.MTooltip,{key:1,content:"This player is a developer.",noUnderline:""},{default:t(()=>[f(u,{class:"text-muted",icon:"user-astronaut",size:"sm"})],void 0,!0),_:1})):g("",!0)])]),"top-right":t(()=>[n(s(e?.id),1)]),"bottom-left":t(()=>[n("Level "+s(e?.level),1)]),"bottom-right":t(()=>[a[5]||(a[5]=n("Joined ")),f(_,{date:e?.createdAt,showAs:"date"},null,8,["date"])]),default:t(()=>[n(s(e?.name),1)],void 0,!0),_:2},1024)):(l(),d(r.MListItem,{key:1,class:"text-danger !tw-px-0 !tw-py-0"},{"top-right":t(()=>[n(s(e?.id),1)]),"bottom-left":t(()=>a[3]||(a[3]=[n("Failed to load player!")])),default:t(()=>[n("🛑 "+s(e?.name),1)],void 0,!0),_:2},1024)):(l(),d(r.MListItem,{key:0,class:"text-muted !tw-px-0 !tw-py-0"},{"top-right":t(()=>[n(s(e?.id),1)]),"bottom-left":t(()=>a[1]||(a[1]=[n("Player not initialized!")])),default:t(()=>[a[2]||(a[2]=n("🚫 Uninitialized"))],void 0,!0),_:2},1024))]),_:1},8,["value"])}const T=M(b,[["render",P],["__file","MetaInputPlayerSelect.vue"]]);export{T as default};
